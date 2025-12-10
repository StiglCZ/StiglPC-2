using System.Text.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

using UserId = int;

namespace StiglPC;

[JsonSourceGenerationOptions(WriteIndented = true, IndentSize = 4)]
[JsonSerializable(typeof(List<StoreUser>))]
[JsonSerializable(typeof(List<Message>))]
[JsonSerializable(typeof(User))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

struct Message {
    [JsonInclude]
    public UserId Author;
    [JsonInclude]
    public DateTime TimeStamp;
    [JsonInclude]
    public string Content;
}

struct StoreUser {
    [JsonInclude]
    public UserId Id;
    [JsonInclude]
    public string Token;
    [JsonInclude]
    public List<Message> MessageQueue;
}

struct User {
    [JsonInclude]
    public readonly UserId Id;
    [JsonInclude]
    public readonly string Token;

    [JsonIgnore]
    private WebSocket? webSocket;
    [JsonIgnore]
    private List<Message> MessageQueue;

    public User() {
        Id = RandomNumberGenerator.GetInt32(UserId.MaxValue);
        Token = RandomNumberGenerator.GetString(TokenChars, TokenLength);
        MessageQueue = new List<Message>();
    }

    public User(StoreUser store) {
        this.Id = store.Id;
        this.Token = store.Token;
        this.MessageQueue = store.MessageQueue;
    }

    public StoreUser Store() {
        StoreUser result = new StoreUser();
        result.Id = this.Id;
        result.Token = this.Token;
        result.MessageQueue = this.MessageQueue;
        return result;
    }

    public void AppendMessage(Message message) {
        MessageQueue.Add(message);

        if(this.webSocket == null)
            return;

        webSocket.SendAsync(new byte[] { 97 }, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public string GetMessages() {
        string result = JsonSerializer.Serialize(MessageQueue, SourceGenerationContext.Default.ListMessage);
        MessageQueue.Clear();
        return result;
    }

    public void AttachWebsocket(WebSocket ws) {
        this.webSocket = ws;
    }

    const int TokenLength = 24;
    const string TokenChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
}

class Storage {
    protected ConcurrentDictionary<UserId, User> Users;
    public Storage() {
        Users = new ConcurrentDictionary<UserId, User>();
    }

    public void Save(string fileName) {
        Console.WriteLine($"Saving to {fileName}...");
        List<StoreUser> UserCollection = Users.Values.Select(x => x.Store()).ToList();

        string JsonTarget = JsonSerializer.Serialize(UserCollection, SourceGenerationContext.Default.ListStoreUser);
        using(StreamWriter writer = new StreamWriter(fileName, false)) {
            writer.Write(JsonTarget);
            writer.Close();
        }
    }

    public void Load(string fileName) {
        Console.WriteLine($"Loading from {fileName}...");
        if(!File.Exists(fileName))
            return;

        string JsonSource;
        using(StreamReader reader = new StreamReader(fileName)) {
            JsonSource = reader.ReadToEnd();
            reader.Close();
        }

        List<StoreUser>? UsersStored = JsonSerializer.Deserialize(JsonSource, SourceGenerationContext.Default.ListStoreUser);
        if(UsersStored == null)
            return;

        UsersStored.ForEach(x => Users.TryAdd(x.Id, new User(x)));
    }

    public User RegisterUser() {
        User result;
        do { result = new User();
        } while(!Users.TryAdd(result.Id, result));

        return result;
    }

    public void DeleteUser(UserId Id)
        => Users.TryRemove(Id, out _);

    public bool AuthenticateUser(UserId Id, string Token) {
        if(!Users.TryGetValue(Id, out User user))
            return false;
        return Token.SequenceEqual(user.Token);
    }

    public void AssignWebsocket(UserId Id, WebSocket ws) {
        Users.AddOrUpdate(Id, (Id) => throw new Exception(), (Id, u) => {
            u.AttachWebsocket(ws);
            return u;
        });
    }

    public void SendMessage(UserId From, UserId To, string Content) {
        Message msg = new Message();
        msg.Author = From;
        msg.TimeStamp = DateTime.Now;
        msg.Content = Content;

        Users[To].AppendMessage(msg);
    }

    public bool UserExists(UserId Id)
        => Users.ContainsKey(Id);

    public string GetMessages(UserId Id)
        => Users[Id].GetMessages();

    public string GetUserList() {
        string result = '[' + String.Join(',', Users.Select(u => u.Value.Id)) + ']';
        return result;
    }
}
