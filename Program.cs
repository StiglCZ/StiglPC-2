namespace StiglPC;

class Program {
    Storage storage;
    Server server;

    bool sigint = false;
    public Program() {
        storage = new Storage();
        server = new Server(storage);
        

        Console.CancelKeyPress += (_, ea) => {
            ea.Cancel = true;
            sigint = true;

            Console.WriteLine("Shutting down gracefully...");
            server.Stop();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            if(!sigint)
                server.Stop();
        };

        server.Start();
        Console.WriteLine("Exiting app...");
    }

    public static void Main(string[] args) => new Program();
}
