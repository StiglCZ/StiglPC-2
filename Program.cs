namespace StiglPC;

class Program {
    public static void Main(string[] args) {
        Storage storage = new Storage();
        Server server = new Server(storage);
        server.Start();
    }
}
