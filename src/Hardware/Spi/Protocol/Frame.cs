namespace PiDash.Hardware.Spi.Protocol;

public enum MsgType : byte
{
    SensorSample = 1,
    CanFrame = 2,
    Dashboard = 3,
    Ack = 250,
    Nack = 251
}

public static class Frame
{
    public const byte Sync0 = 0xA5;
    public const byte Sync1 = 0x5A;
    public const int HeaderLen = 2 + 1 + 1 + 2; // sync(2) + type(1) + seq(1) + len(2)
    public const int CrcLen = 2;
    public const int MaxPayload = 1024;

    // Writes frame into dest. Returns total frame length.
    public static int Write(Span<byte> dest, MsgType type, byte seq, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayload) throw new ArgumentOutOfRangeException(nameof(payload));

        var totalLen = HeaderLen + payload.Length + CrcLen;
        if (dest.Length < totalLen) 
            throw new ArgumentException($"Destination buffer must be at least {totalLen} bytes, but is only {dest.Length} bytes");

        dest[0] = Sync0;
        dest[1] = Sync1;
        dest[2] = (byte)type;
        dest[3] = seq;
        dest[4] = (byte)(payload.Length & 0xFF);
        dest[5] = (byte)((payload.Length >> 8) & 0xFF);

        payload.CopyTo(dest.Slice(HeaderLen));

        var crc = Crc16.Compute(dest.Slice(0, HeaderLen + payload.Length));
        dest[HeaderLen + payload.Length + 0] = (byte)(crc & 0xFF);
        dest[HeaderLen + payload.Length + 1] = (byte)((crc >> 8) & 0xFF);

        return totalLen;
    }

    // Tries to parse a complete frame from src. Returns true if valid and complete.
    public static bool TryParse(ReadOnlySpan<byte> src, out MsgType type, out byte seq, out ReadOnlySpan<byte> payload, out int totalLen)
    {
        type = default;
        seq = default;
        payload = default;
        totalLen = 0;

        if (src.Length < HeaderLen + CrcLen) return false;
        if (src[0] != Sync0 || src[1] != Sync1) return false;

        type = (MsgType)src[2];
        seq = src[3];
        int len = src[4] | (src[5] << 8);

        if (len < 0 || len > MaxPayload) return false;

        totalLen = HeaderLen + len + CrcLen;
        if (src.Length < totalLen) return false;

        var expected = (ushort)(src[HeaderLen + len] | (src[HeaderLen + len + 1] << 8));
        var actual = Crc16.Compute(src.Slice(0, HeaderLen + len));
        if (expected != actual) return false;

        payload = src.Slice(HeaderLen, len);
        return true;
    }
}
