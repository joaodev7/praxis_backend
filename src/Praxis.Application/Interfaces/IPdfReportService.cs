using Praxis.Domain.Entities;

namespace Praxis.Application.Interfaces;

public interface IPdfReportService
{
    byte[] GenerateVisitReportPdf(Visit visit);
}
