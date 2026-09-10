namespace Praxis.Application.Interfaces;

public interface IQrCodeGenerator
{
    byte[] GenerateQrCodePng(string content, int pixelsPerModule = 4);
}
