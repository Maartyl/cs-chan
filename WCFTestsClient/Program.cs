// Client
using System;
using System.ServiceModel;

namespace Client
{
  public class Program {
    public static void Main(string[] args) {
      Console.WriteLine("WCF Client\n");

      var ip = args.Length == 0 ? "localhost" : args[0];

      var binding = new BasicHttpBinding();
      var address = new EndpointAddress("http://" + ip + ":8080");
      var client = new MyServiceClient(binding, address);

      while (true) {
        Console.Write("\nEnter name: ");
        var name = Console.ReadLine();
        if (name == null)
          break;

        Console.WriteLine("Service response: " + client.Greet(name));
      }
    }
  }
}