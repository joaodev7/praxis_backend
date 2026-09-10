using Praxis.Domain.Entities;

namespace Praxis.Application.Interfaces;

public interface ILabelPdfService
{
    byte[] GenerateSingleLabelPdf(FoodLabel label, string templateType = "Thermal80x50", int copies = 1);
    byte[] GenerateBulkLabelsPdf(IEnumerable<FoodLabel> labels, string templateType = "Thermal80x50", int copiesPerLabel = 1);
    byte[] GenerateA4SheetPdf(IEnumerable<FoodLabel> labels);
}
