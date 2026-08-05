using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace SimulcastUtility.Wpf.Services
{
    public static partial class MarkdownFlowDocumentRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        public static FlowDocument Render(string markdown)
        {
            FlowDocument document = CreateDocument();
            MarkdownDocument markdownDocument = Markdown.Parse(markdown, Pipeline);

            foreach (Markdig.Syntax.Block block in markdownDocument)
                AddBlock(document.Blocks, block);

            return document;
        }

        public static FlowDocument CreateMessage(string message)
        {
            FlowDocument document = CreateDocument();
            Paragraph paragraph = new(new Run(message)) { Margin = new Thickness(0), FontSize = 12 };
            SetResource(paragraph, TextElement.ForegroundProperty, "SecondaryTextBrush");
            document.Blocks.Add(paragraph);
            return document;
        }

        public static FlowDocument CreateError(string message)
        {
            FlowDocument document = CreateDocument();
            Paragraph paragraph = new(new Run(message)) { Margin = new Thickness(0), FontSize = 12 };
            SetResource(paragraph, TextElement.ForegroundProperty, "ErrorBrush");
            document.Blocks.Add(paragraph);
            return document;
        }

        private static FlowDocument CreateDocument()
        {
            FlowDocument document = new()
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                LineHeight = 20
            };
            SetResource(document, TextElement.ForegroundProperty, "PrimaryTextBrush");
            return document;
        }

        private static void AddBlock(BlockCollection blocks, Markdig.Syntax.Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    blocks.Add(CreateHeading(heading));
                    break;
                case ParagraphBlock paragraph:
                    blocks.Add(CreateParagraph(paragraph));
                    break;
                case ListBlock list:
                    blocks.Add(CreateList(list));
                    break;
                case QuoteBlock quote:
                    blocks.Add(CreateQuote(quote));
                    break;
                case CodeBlock code:
                    blocks.Add(CreateCodeBlock(code));
                    break;
                case ThematicBreakBlock:
                    blocks.Add(CreateRule());
                    break;
                case ContainerBlock container:
                    Section section = new() { Margin = new Thickness(0) };

                    foreach (Markdig.Syntax.Block child in container)
                        AddBlock(section.Blocks, child);

                    blocks.Add(section);
                    break;
            }
        }

        private static Paragraph CreateHeading(HeadingBlock heading)
        {
            double fontSize = heading.Level switch
            {
                1 => 23,
                2 => 19,
                3 => 16,
                _ => 14
            };
            Paragraph paragraph = new() { FontSize = fontSize, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, heading.Level == 1 ? 0 : 14, 0, 8), LineHeight = fontSize + 6 };
            AddInlines(paragraph.Inlines, heading.Inline);
            SetResource(paragraph, TextElement.ForegroundProperty, "PrimaryTextBrush");
            return paragraph;
        }

        private static Paragraph CreateParagraph(ParagraphBlock paragraphBlock)
        {
            Paragraph paragraph = new() { Margin = new Thickness(0, 0, 0, 10) };
            AddInlines(paragraph.Inlines, paragraphBlock.Inline);
            return paragraph;
        }

        private static System.Windows.Documents.List CreateList(ListBlock listBlock)
        {
            System.Windows.Documents.List list = new()
            {
                MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                Margin = new Thickness(18, 2, 0, 12),
                Padding = new Thickness(4, 0, 0, 0)
            };

            foreach (ListItemBlock itemBlock in listBlock.OfType<ListItemBlock>())
            {
                ListItem item = new() { Margin = new Thickness(0, 2, 0, 3) };

                foreach (Markdig.Syntax.Block child in itemBlock)
                    AddBlock(item.Blocks, child);

                list.ListItems.Add(item);
            }

            return list;
        }

        private static Section CreateQuote(QuoteBlock quoteBlock)
        {
            Section section = new()
            {
                Margin = new Thickness(0, 6, 0, 14),
                Padding = new Thickness(14, 10, 12, 2),
                BorderThickness = new Thickness(3, 0, 0, 0)
            };
            SetResource(section, System.Windows.Documents.Block.BorderBrushProperty, "AccentBrush");
            SetResource(section, TextElement.ForegroundProperty, "SecondaryTextBrush");

            foreach (Markdig.Syntax.Block child in quoteBlock)
                AddBlock(section.Blocks, child);

            return section;
        }

        private static Paragraph CreateCodeBlock(CodeBlock codeBlock)
        {
            Paragraph paragraph = new(new Run(codeBlock.Lines.ToString()))
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 14),
                Padding = new Thickness(12, 10, 12, 10),
                LineHeight = 18
            };
            SetResource(paragraph, TextElement.BackgroundProperty, "InputBackgroundBrush");
            SetResource(paragraph, TextElement.ForegroundProperty, "PrimaryTextBrush");
            return paragraph;
        }

        private static BlockUIContainer CreateRule()
        {
            Border border = new() { Height = 1, Margin = new Thickness(0, 10, 0, 16) };
            border.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            return new BlockUIContainer(border);
        }

        private static void AddInlines(InlineCollection destination, ContainerInline? container)
        {
            if (container is null)
                return;

            for (Markdig.Syntax.Inlines.Inline? inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
            {
                switch (inline)
                {
                    case LiteralInline literal:
                        destination.Add(new Run(literal.Content.ToString()));
                        break;
                    case EmphasisInline emphasis:
                        Span emphasisSpan = new() { FontWeight = emphasis.DelimiterCount >= 2 ? FontWeights.SemiBold : FontWeights.Normal, FontStyle = emphasis.DelimiterCount == 1 ? FontStyles.Italic : FontStyles.Normal };
                        AddInlines(emphasisSpan.Inlines, emphasis);
                        destination.Add(emphasisSpan);
                        break;
                    case CodeInline code:
                        Run codeRun = new(code.Content) { FontFamily = new FontFamily("Consolas"), FontSize = 11 };
                        SetResource(codeRun, TextElement.BackgroundProperty, "InputBackgroundBrush");
                        destination.Add(codeRun);
                        break;
                    case LineBreakInline lineBreak:
                        destination.Add(new LineBreak());

                        if (lineBreak.IsHard)
                            destination.Add(new LineBreak());
                        break;
                    case LinkInline link:
                        destination.Add(CreateLink(link));
                        break;
                    case HtmlInline html:
                        destination.Add(new Run(WebUtility.HtmlDecode(HtmlTagRegex().Replace(html.Tag, string.Empty))));
                        break;
                    case ContainerInline childContainer:
                        Span span = new();
                        AddInlines(span.Inlines, childContainer);
                        destination.Add(span);
                        break;
                }
            }
        }

        private static System.Windows.Documents.Inline CreateLink(LinkInline link)
        {
            string url = link.GetDynamicUrl is null ? link.Url ?? string.Empty : link.GetDynamicUrl();

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
            {
                Span contents = new();
                AddInlines(contents.Inlines, link);
                return contents;
            }

            Hyperlink hyperlink = new() { NavigateUri = uri };
            AddInlines(hyperlink.Inlines, link);
            hyperlink.RequestNavigate += OpenHyperlink;
            SetResource(hyperlink, TextElement.ForegroundProperty, "AccentBrush");
            return hyperlink;
        }

        private static void OpenHyperlink(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private static void SetResource(FrameworkContentElement element, DependencyProperty property, object resourceKey)
        {
            element.SetResourceReference(property, resourceKey);
        }

        [GeneratedRegex("<[^>]+>")]
        private static partial Regex HtmlTagRegex();
    }
}
