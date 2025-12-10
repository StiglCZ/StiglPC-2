using System.Net;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;
using UserId = int;

namespace StiglPC;

class Server {
    Storage storage;
    HttpListener server;

    public Server(Storage storage, string ListenOn) {
        this.storage = storage;
        server = new HttpListener();
        server.Prefixes.Add(ListenOn);
    }

    private void RespondWithStatus(HttpStatusCode code, HttpListenerResponse res, string? body = null) {
        res.StatusCode = (int)code;
        res.StatusDescription = code.ToString();

        res.Headers.Remove("Server");
        if(body != null) {
            res.ContentLength64 = Encoding.UTF8.GetByteCount(body);
            res.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
        }

        res.OutputStream.Close();
        res.Close();
    }

    private async Task HandleWebsocket(HttpListenerContext c, UserId Id) {
        if(!c.Request.IsWebSocketRequest) {
            RespondWithStatus(HttpStatusCode.BadRequest, c.Response);
            return;
        }

        HttpListenerWebSocketContext wsc =
            await c.AcceptWebSocketAsync(subProtocol: null);

        storage.AssignWebsocket(Id, wsc.WebSocket);
    }

    private void HandleRegisterUser(HttpListenerContext c) {
        User u = storage.RegisterUser();
        string result = JsonSerializer.Serialize(u, SourceGenerationContext.Default.User);

        RespondWithStatus(HttpStatusCode.OK, c.Response, body: result);
    }

    private void HandleDeleteUser(HttpListenerContext c, UserId Id) {
        storage.DeleteUser(Id);
        RespondWithStatus(HttpStatusCode.OK, c.Response);
    }

    private void HandleGetMessages(HttpListenerContext c, UserId Id) {
        string result = storage.GetMessages(Id);
        RespondWithStatus(HttpStatusCode.OK, c.Response, body: result);
    }

    private void HandleSendMessage(HttpListenerContext c, UserId Id) {
        string? TargetString = c.Request.Headers["Target"];
        bool TargetParsed = UserId.TryParse(TargetString, out UserId Target);

        if(!TargetParsed || !storage.UserExists(Target)) {
            RespondWithStatus(HttpStatusCode.BadRequest, c.Response);
            return;
        }

        string Content;

        using(StreamReader reader = new StreamReader(c.Request.InputStream, c.Request.ContentEncoding))
            Content = reader.ReadToEnd();

        if(Content.Length <= 0) {
            RespondWithStatus(HttpStatusCode.BadRequest, c.Response);
            return;
        }

        if(!TargetParsed) {
            RespondWithStatus(HttpStatusCode.NotFound, c.Response);
            return;
        }

        storage.SendMessage(Id, Target, Content);
        RespondWithStatus(HttpStatusCode.Accepted, c.Response);
    }

    private void HandleClient(HttpListenerContext c) {
        string? IdString = c.Request.Headers["Id"];
        string? TokenString = c.Request.Headers["Token"];

        bool IdParsed = UserId.TryParse(IdString, out UserId Id);
        bool Authenticated = IdParsed && TokenString != null && storage.AuthenticateUser(Id, TokenString!);

        switch(c.Request.HttpMethod + ' ' + c.Request.RawUrl) {
            case "PUT /users" or "GET /register":
                HandleRegisterUser(c);
                break;

            case "GET /users":
                RespondWithStatus(HttpStatusCode.OK, c.Response, storage.GetUserList());
                break;

            case "DELETE /users" or "GET /delete":
                if(Authenticated)
                    HandleDeleteUser(c, Id);
                else RespondWithStatus(HttpStatusCode.Unauthorized, c.Response);
                break;

            case "GET /messages":
                if(Authenticated)
                    HandleGetMessages(c, Id);
                else RespondWithStatus(HttpStatusCode.Unauthorized, c.Response);
                break;

            case "PUT /messages" or "GET /send":
                if(Authenticated)
                    HandleSendMessage(c, Id);
                else RespondWithStatus(HttpStatusCode.Unauthorized, c.Response);
                break;

            case "GET /ws":
                if(Authenticated)
                    Task.Run(() => HandleWebsocket(c, Id));
                else RespondWithStatus(HttpStatusCode.Unauthorized, c.Response);
                break;
            default:
                Console.WriteLine("Not found");
                RespondWithStatus(HttpStatusCode.BadRequest, c.Response);
                break;
        }
    }

    public void Start() {
        try {
            server.Start();
            while(server.IsListening) {
                HttpListenerContext context =
                    server.GetContext();

                Task.Run(() => HandleClient(context));
            }
        } catch (HttpListenerException e)
            when (e.ErrorCode == 500) { // 500 - Listener aborted
                Console.WriteLine($"Listener aborted : {e.Message}");
        }
    }

    public void Stop() {
        server.Abort();
    }
}
