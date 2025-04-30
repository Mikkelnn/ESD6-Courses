using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BFSAlgo.Distributed.Network
{
    public interface ITcpListener
    {
        Task<TcpClient> AcceptTcpClientAsync();
        void Start();
        EndPoint LocalEndpoint { get; }
    }

    public class TcpListenerWrapper(IPAddress localaddr, int port) : TcpListener(localaddr, port), ITcpListener
    {
    }
}
