//连接管理器（连接池）存的Connection
using System.Collections.Concurrent;

namespace BlackCatRabbitServer
{
    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<long, Connection> _connections = new();
        public void Add(Connection conn)
        {
            _connections[conn.ConnectionId] = conn;
        }
        public void Remove(long id)
        {
            _connections.TryRemove(id, out _);
        }
        public Connection Get(long id)
        { 
            return _connections.GetValueOrDefault(id);
        }
        public void CloseAll()
        {
            foreach (var conn in _connections.Values)
            {
                conn.Close();
            }
            _connections.Clear();
        }
        public int Count => _connections.Count;

    }
}
