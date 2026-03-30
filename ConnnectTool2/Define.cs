using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnnectTool2
{
    class ServerConfig
    {
        public string Name { get; set; }
        public string SubName { get; set; }
        public List<Connection> Connections { get; set; } = new List<Connection>();
    }

    class Connection
    {
        public int LisOrConn { get; set; }
        public int Soc { get; set; }
        public string Comment { get; set; }
        public string LocalIP { get; set; }
        public string ConnectIP { get; set; }
        public int GroupNumber { get; set; }
        public int Port { get; set; }
        public int RecvBuffer { get; set; }
        public int SendBuffer { get; set; }
        public int ReadQueueBuffer { get; set; }
        public int SendQueueBuffer { get; set; }
    }
}
