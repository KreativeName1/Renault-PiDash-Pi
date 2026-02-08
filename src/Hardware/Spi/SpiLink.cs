using System.Device.Spi;
using PiDash.Hardware.Spi.Protocol;

namespace PiDash.Hardware.Spi;

public sealed class SpiLink : IDisposable
{
    private readonly SpiDevice _spi;
    private byte _seq;

    public SpiLink(SpiDevice spi) => _spi = spi;

    public void Dispose() => _spi.Dispose();

    // Simple: full-duplex transfer. MCU must reply with a frame (or zeroed data).
    public int Transfer(ReadOnlySpan<byte> tx, Span<byte> rx)
    {
        _spi.TransferFullDuplex(tx, rx);
        return rx.Length;
    }

    // Convenience: write a frame, read reply frame into rxBuf.
    public bool SendAndReceive(MsgType type, ReadOnlySpan<byte> payload, Span<byte> txBuf, Span<byte> rxBuf,
        out MsgType rType, out ReadOnlySpan<byte> rPayload)
    {
        rType = default;
        rPayload = default;

        var frameLen = Frame.Write(txBuf, type, _seq++, payload);

        // Send tx frame; read rx of same length (simple for phase 1)
        rxBuf.Slice(0, frameLen).Clear();
        _spi.TransferFullDuplex(txBuf.Slice(0, frameLen), rxBuf.Slice(0, frameLen));

        if (!Frame.TryParse(rxBuf.Slice(0, frameLen), out var rt, out _, out var rp, out _))
            return false;

        rType = rt;
        rPayload = rp;
        return true;
    }
}