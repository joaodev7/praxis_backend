using Praxis.Application.Interfaces;
using QRCoder;

namespace Praxis.Infrastructure.Services;

public class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] GenerateQrCodePng(string content, int pixelsPerModule = 4)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<byte>();

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
