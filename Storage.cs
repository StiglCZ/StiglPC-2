using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

using UserId = int;

namespace StiglPC;

[JsonSourceGenerationOptions(WriteIndented = true, IndentSize = 4)]
[JsonSerializable(typeof(List<Message>))]
[JsonSerializable(typeof(Message))]
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

    public User(UserId Id, string Token) {
        this.Id = Id;
        this.Token = Token;
        MessageQueue = new List<Message>();
    }

    public void AppendMessage(Message message) =>
        MessageQueue.Add(message);

    public string GetMessages() =>
        JsonSerializer.Serialize(MessageQueue, SourceGenerationContext.Default.ListMessage);

    public void AttachWebsocket(WebSocket ws) {
        webSocket = ws;
    }

    const int TokenLength = 24;
    const string TokenChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
}

class Storage {
    protected Dictionary<UserId, User> Users;
    public Storage() {
        Users = new Dictionary<UserId, User>();
    }

    public User RegisterUser() {
        User result;
        do { result = new User();
        } while(Users.ContainsKey(result.Id));

        Users.Add(result.Id, result);
        return result;
    }

    public void DeleteUser(UserId Id) =>
        Users.Remove(Id);

    public bool AuthenticateUser(UserId Id, string Token) {
        if(!Users.ContainsKey(Id))
            return false;
        return Token.SequenceEqual(Users[Id].Token);
    }

    public void AssignWebsocket(UserId Id, WebSocket ws) =>
        Users[Id].AttachWebsocket(ws);

    public void SendMessage(UserId From, UserId To, string Content) {
        Message msg = new Message();
        msg.Author = From;
        msg.TimeStamp = DateTime.Now;
        msg.Content = Content;

        Users[To].AppendMessage(msg);
    }

    public bool UserExists(UserId Id) =>
        Users.ContainsKey(Id);

    public string GetMessages(UserId Id) =>
        Users[Id].GetMessages();

    public string GetUserList() {
        string result = '[' + String.Join(',', Users.Select(u => u.Value.Id)) + ']';
        return result;
    }
    
}
