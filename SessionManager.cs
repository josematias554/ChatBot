using QuillenBot.Models;

namespace QuillenBot.Services;

public class SessionManager
{
    private readonly Dictionary<long, ConversationSession> _sessions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<ConversationSession> ObtenerOCrearSesionAsync(long chatId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(chatId, out var session))
            {
                session = new ConversationSession { ChatId = chatId };
                _sessions[chatId] = session;
            }
            return session;
        }
        finally { _lock.Release(); }
    }

    public async Task EliminarSesionAsync(long chatId)
    {
        await _lock.WaitAsync();
        try { _sessions.Remove(chatId); }
        finally { _lock.Release(); }
    }

    public async Task<ConversationSession?> ObtenerSesionAsync(long chatId)
    {
        await _lock.WaitAsync();
        try { return _sessions.TryGetValue(chatId, out var s) ? s : null; }
        finally { _lock.Release(); }
    }

    // Guarda pedidos pendientes para correlacionar aprobaciones
    private readonly Dictionary<int, (PedidoCliente pedido, long chatIdCliente)> _pedidosPendientes = new();
    private readonly SemaphoreSlim _lockPedidos = new(1, 1);

    public async Task RegistrarPedidoPendienteAsync(PedidoCliente pedido, long chatIdCliente)
    {
        await _lockPedidos.WaitAsync();
        try { _pedidosPendientes[pedido.Id] = (pedido, chatIdCliente); }
        finally { _lockPedidos.Release(); }
    }

    public async Task<(PedidoCliente? pedido, long chatIdCliente)> ObtenerPedidoPendienteAsync(int pedidoId)
    {
        await _lockPedidos.WaitAsync();
        try
        {
            if (_pedidosPendientes.TryGetValue(pedidoId, out var val))
                return val;
            return (null, 0);
        }
        finally { _lockPedidos.Release(); }
    }

    public async Task RemoverPedidoPendienteAsync(int pedidoId)
    {
        await _lockPedidos.WaitAsync();
        try { _pedidosPendientes.Remove(pedidoId); }
        finally { _lockPedidos.Release(); }
    }
}
