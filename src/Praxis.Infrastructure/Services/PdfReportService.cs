using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;

namespace Praxis.Infrastructure.Services;

public class PdfReportService : IPdfReportService
{
    public PdfReportService()
    {
        // QuestPDF license configuration
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateVisitReportPdf(Visit visit)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, visit));
                page.Content().Element(c => ComposeContent(c, visit));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, Visit visit)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("PRAXIS").FontSize(20).Bold().FontColor("#2563EB");
                col.Item().Text("Relatório Técnico de Visita e Conformidade").FontSize(13).SemiBold();
                col.Item().Text($"Gerado em {DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)").FontSize(8).FontColor(Colors.Grey.Medium);
            });

            row.ConstantItem(120).Column(col =>
            {
                var conforming = visit.Items.Count(i => i.Result == EvaluationResult.Conforme);
                var nonConforming = visit.Items.Count(i => i.Result == EvaluationResult.NaoConforme);
                var total = conforming + nonConforming;
                var rate = total > 0 ? Math.Round((double)conforming / total * 100, 1) : 100.0;

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(c =>
                {
                    c.Item().AlignCenter().Text("Conformidade").FontSize(8).SemiBold();
                    c.Item().AlignCenter().Text($"{rate}%").FontSize(18).Bold().FontColor(rate >= 80 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });
            });
        });
    }

    private void ComposeContent(IContainer container, Visit visit)
    {
        container.PaddingVertical(10).Column(col =>
        {
            // 1. Identification Box
            col.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(box =>
            {
                box.Item().Text("1. Dados da Operação").Bold().FontSize(11);
                box.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Empresa Cliente: {visit.Unit?.ClientCompany?.TradeName ?? "N/A"}").SemiBold();
                    r.RelativeItem().Text($"Unidade: {visit.Unit?.Name ?? "N/A"}").SemiBold();
                });
                box.Item().Row(r =>
                {
                    r.RelativeItem().Text($"Nutricionista Responsável: {visit.Nutritionist?.User?.Name ?? "N/A"} (CRN: {visit.Nutritionist?.Crn ?? "N/A"})");
                    r.RelativeItem().Text($"Data da Visita: {visit.ScheduledAt:dd/MM/yyyy}");
                });
                if (!string.IsNullOrWhiteSpace(visit.Unit?.Address))
                {
                    box.Item().Text($"Endereço: {visit.Unit.Address}");
                }
            });

            col.Item().PaddingTop(15).Text("2. Avaliação dos Itens do Checklist").Bold().FontSize(12);

            // Table of items
            col.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Categoria").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Item / Critério").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Resultado").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text("Observação").Bold();
                });

                foreach (var item in visit.Items)
                {
                    var resultText = item.Result switch
                    {
                        EvaluationResult.Conforme => "Conforme",
                        EvaluationResult.NaoConforme => "Não Conforme",
                        _ => "N/A"
                    };

                    var resultColor = item.Result switch
                    {
                        EvaluationResult.Conforme => Colors.Green.Darken2,
                        EvaluationResult.NaoConforme => Colors.Red.Darken2,
                        _ => Colors.Grey.Darken1
                    };

                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.ChecklistItem?.Category ?? "-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.ChecklistItem?.Description ?? "-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(resultText).Bold().FontColor(resultColor);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(item.Observation ?? "-");
                }
            });

            // 3. Non-Conformities section
            if (visit.NonConformities.Any(nc => !nc.IsDeleted))
            {
                col.Item().PaddingTop(20).Text("3. Não Conformidades e Ações Corretivas").Bold().FontSize(12);

                foreach (var nc in visit.NonConformities.Where(nc => !nc.IsDeleted))
                {
                    col.Item().PaddingTop(8).Border(1).BorderColor(Colors.Red.Lighten2).Padding(8).Column(ncBox =>
                    {
                        ncBox.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"[{nc.Category}] {nc.Description}").Bold().FontColor(Colors.Red.Darken2);
                            r.ConstantItem(120).AlignRight().Text($"Gravidade: {nc.Severity}").SemiBold();
                        });

                        if (!string.IsNullOrWhiteSpace(nc.CorrectiveAction))
                        {
                            ncBox.Item().PaddingTop(4).Text($"Ação Corretiva Recomendada: {nc.CorrectiveAction}").Italic();
                        }

                        if (nc.DueDate.HasValue)
                        {
                            ncBox.Item().PaddingTop(2).Text($"Prazo para Regularização: {nc.DueDate.Value:dd/MM/yyyy}");
                        }
                    });
                }
            }

            // 4. Notes
            if (!string.IsNullOrWhiteSpace(visit.Notes))
            {
                col.Item().PaddingTop(15).Column(c =>
                {
                    c.Item().Text("4. Observações Finais").Bold().FontSize(11);
                    c.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(visit.Notes);
                });
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("PRAXIS — Sistema Integrado de Responsabilidade Técnica em Nutrição").FontSize(8).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.CurrentPageNumber();
                x.Span(" / ");
                x.TotalPages();
            });
        });
    }
}
