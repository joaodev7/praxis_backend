using Microsoft.Extensions.Configuration;
using Praxis.Application.Interfaces;
using Praxis.Domain.Entities;
using Praxis.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Praxis.Infrastructure.Services;

public class LabelPdfService : ILabelPdfService
{
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly string _publicBaseUrl;

    public LabelPdfService(IQrCodeGenerator qrCodeGenerator, IConfiguration configuration)
    {
        _qrCodeGenerator = qrCodeGenerator;
        _publicBaseUrl = configuration["APP_PUBLIC_URL"] 
            ?? configuration["App:PublicUrl"] 
            ?? "https://app.praxisnutri.com.br";
        
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateSingleLabelPdf(FoodLabel label, string templateType = "Thermal80x50", int copies = 1)
    {
        return GenerateBulkLabelsPdf(new[] { label }, templateType, copies);
    }

    public byte[] GenerateBulkLabelsPdf(IEnumerable<FoodLabel> labels, string templateType = "Thermal80x50", int copiesPerLabel = 1)
    {
        var labelList = labels.ToList();
        if (!labelList.Any()) return Array.Empty<byte>();

        if (templateType.Equals("SheetA4", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateA4SheetPdf(labelList);
        }

        var document = Document.Create(container =>
        {
            foreach (var label in labelList)
            {
                int times = copiesPerLabel > 0 ? copiesPerLabel : 1;
                for (int i = 0; i < times; i++)
                {
                    if (templateType.Equals("Thermal50x30", StringComparison.OrdinalIgnoreCase))
                    {
                        container.Page(page => ComposeThermal50x30(page, label));
                    }
                    else
                    {
                        // Default: Thermal80x50
                        container.Page(page => ComposeThermal80x50(page, label));
                    }
                }
            }
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateA4SheetPdf(IEnumerable<FoodLabel> labels)
    {
        var labelList = labels.ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    foreach (var label in labelList)
                    {
                        table.Cell().Padding(3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Element(e => ComposeA4LabelCell(e, label));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeThermal80x50(PageDescriptor page, FoodLabel label)
    {
        // 80mm x 50mm
        page.Size(80, 50, QuestPDF.Infrastructure.Unit.Millimetre);
        page.Margin(2.5f, QuestPDF.Infrastructure.Unit.Millimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(7).FontFamily("Arial").FontColor(Colors.Black));

        byte[] qrBytes = GetQrCodeBytes(label.PublicToken);

        page.Content().Column(col =>
        {
            // Cabeçalho da Etiqueta
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("PRAXIS • IDENTIFICAÇÃO DE ALIMENTO").FontSize(6).Bold().FontColor("#1E40AF");
                    c.Item().Text(label.Unit?.Name ?? "Unidade").FontSize(5.5f).FontColor(Colors.Grey.Darken2);
                });

                r.AutoItem().Text(FormatOperationName(label.OperationType).ToUpper()).FontSize(6).Bold().FontColor(Colors.Grey.Darken3);
            });

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

            // Nome do Produto em Destaque
            col.Item().PaddingTop(1).PaddingBottom(1).Text(label.Product?.Name ?? label.Description)
                .FontSize(9).Bold().FontColor(Colors.Black);

            // Corpo: Dados Operacionais à esquerda, QR Code à direita
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("Manipulação: ").SemiBold();
                        t.Span($"{label.ValidityStartAt:dd/MM/yyyy HH:mm}");
                    });

                    c.Item().PaddingVertical(1).Background(Colors.Grey.Lighten4).Padding(2).Border(0.5f).BorderColor(Colors.Grey.Medium).Column(valBox =>
                    {
                        valBox.Item().Text(t =>
                        {
                            t.Span("VALIDADE: ").Bold().FontSize(7.5f).FontColor(Colors.Red.Darken3);
                            t.Span($"{label.EffectiveExpirationDate:dd/MM/yyyy HH:mm}").Bold().FontSize(7.5f).FontColor(Colors.Red.Darken3);
                        });
                    });

                    c.Item().Text(t =>
                    {
                        t.Span("Conservação: ").SemiBold();
                        t.Span(FormatStorageCondition(label.StorageCondition, label.StorageTemperatureMax));
                    });

                    c.Item().Text(t =>
                    {
                        t.Span("Lote: ").SemiBold();
                        t.Span(label.InternalBatchCode).Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(label.CreatedByUser?.Name))
                    {
                        c.Item().Text($"Resp: {label.CreatedByUser.Name}").FontSize(5.5f).FontColor(Colors.Grey.Darken2);
                    }
                });

                // QR Code
                if (qrBytes.Length > 0)
                {
                    row.ConstantItem(38).AlignCenter().Column(qc =>
                    {
                        qc.Item().Width(32).Height(32).Image(qrBytes);
                        qc.Item().AlignCenter().Text("Validação QR").FontSize(4.5f).FontColor(Colors.Grey.Darken1);
                    });
                }
            });

            // Rodapé
            col.Item().AlignBottom().Row(rf =>
            {
                rf.RelativeItem().Text("RDC 216/2004 • Controle Sanitário").FontSize(4.5f).FontColor(Colors.Grey.Medium);
                rf.AutoItem().Text(label.InternalBatchCode).FontSize(4.5f).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeThermal50x30(PageDescriptor page, FoodLabel label)
    {
        // 50mm x 30mm
        page.Size(50, 30, QuestPDF.Infrastructure.Unit.Millimetre);
        page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Millimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(5.5f).FontFamily("Arial").FontColor(Colors.Black));

        byte[] qrBytes = GetQrCodeBytes(label.PublicToken);

        page.Content().Column(col =>
        {
            // Nome do Produto
            col.Item().Text(label.Product?.Name ?? label.Description).FontSize(7).Bold();

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Data: {label.ValidityStartAt:dd/MM/yy HH:mm}");
                    c.Item().Text($"VAL: {label.EffectiveExpirationDate:dd/MM/yy HH:mm}").Bold().FontColor(Colors.Red.Darken2);
                    c.Item().Text($"Cons: {FormatStorageCondition(label.StorageCondition, label.StorageTemperatureMax)}");
                    c.Item().Text($"Lote: {label.InternalBatchCode}").Bold();
                });

                if (qrBytes.Length > 0)
                {
                    row.ConstantItem(22).AlignCenter().Column(qc =>
                    {
                        qc.Item().Width(20).Height(20).Image(qrBytes);
                    });
                }
            });

            col.Item().AlignBottom().Text("PRAXIS • RDC 216").FontSize(4.5f).FontColor(Colors.Grey.Medium);
        });
    }

    private void ComposeA4LabelCell(IContainer container, FoodLabel label)
    {
        byte[] qrBytes = GetQrCodeBytes(label.PublicToken);

        container.Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Text(label.Product?.Name ?? label.Description).FontSize(8).Bold();
                r.AutoItem().Text(FormatOperationName(label.OperationType)).FontSize(6).FontColor(Colors.Grey.Darken2);
            });

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Manipulado: {label.ValidityStartAt:dd/MM/yyyy HH:mm}").FontSize(6.5f);
                    c.Item().Text($"VALIDADE: {label.EffectiveExpirationDate:dd/MM/yyyy HH:mm}").FontSize(7).Bold().FontColor(Colors.Red.Darken3);
                    c.Item().Text($"Conservação: {FormatStorageCondition(label.StorageCondition, label.StorageTemperatureMax)}").FontSize(6.5f);
                    c.Item().Text($"Lote: {label.InternalBatchCode}").FontSize(6.5f).Bold();
                });

                if (qrBytes.Length > 0)
                {
                    row.ConstantItem(28).AlignCenter().Column(qc =>
                    {
                        qc.Item().Width(24).Height(24).Image(qrBytes);
                    });
                }
            });
        });
    }

    private byte[] GetQrCodeBytes(string publicToken)
    {
        if (string.IsNullOrWhiteSpace(publicToken))
            return Array.Empty<byte>();

        string url = $"{_publicBaseUrl.TrimEnd('/')}/public/labels/{publicToken}";
        return _qrCodeGenerator.GenerateQrCodePng(url, 3);
    }

    private static string FormatOperationName(LabelOperationType op) => op switch
    {
        LabelOperationType.Preparation => "Preparo",
        LabelOperationType.Opening => "Abertura",
        LabelOperationType.Portioning => "Fracionamento",
        LabelOperationType.Defrosting => "Descongelamento",
        LabelOperationType.PrePreparation => "Pré-preparo",
        LabelOperationType.Storage => "Armazenamento",
        _ => op.ToString()
    };

    private static string FormatStorageCondition(StorageCondition cond, decimal? maxTemp)
    {
        string name = cond switch
        {
            StorageCondition.Ambient => "Ambiente",
            StorageCondition.Refrigerated => "Refrigerado",
            StorageCondition.Frozen => "Congelado",
            StorageCondition.Heated => "Aquecido / Estufa",
            _ => "Outro"
        };

        if (maxTemp.HasValue)
        {
            return $"{name} (≤ {maxTemp.Value:0.#} °C)";
        }

        return name;
    }
}
