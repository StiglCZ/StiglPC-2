namespace StiglPC;

class Program {
    Storage storage;
    Server server;

    bool sigint = false;
    string SaveFile = "users.json";
    string ListenOn = "http://127.0.0.1:8080/";

    public Program(string[] args) {
        // Parse args to get the port and file for saves

        storage = new Storage();
        storage.Load(SaveFile);

        server = new Server(storage, ListenOn);

        Console.CancelKeyPress += (_, ea) => {
            ea.Cancel = true;
            sigint = true;

            Console.WriteLine("Shutting down gracefully...");
            server.Stop();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            if(!sigint) {
                Console.WriteLine("Shutting down gracefully...");
                server.Stop();
            }
        };

        Console.WriteLine($"Listening on {ListenOn}");
        server.Start();
        Console.WriteLine("Exiting app...");
        storage.Save(SaveFile);
    }

    public static void Main(string[] args)
        => new Program(args);
}
