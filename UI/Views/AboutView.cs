using System;
using System.Diagnostics;

using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;

using Microsoft.Xna.Framework;

using MonoGame.Extended.BitmapFonts;

using SongbookOfTyria.UI.Controls.Notation;

namespace SongbookOfTyria.UI.Views
{
    public class AboutView : View
    {
        private const int LeftPadding = 70;
        private const int DefaultSpacing = 8;
        private const int SectionSpacing = 20;

        private Panel _scrollPanel;
        private FlowPanel _aboutPanel;

        protected override void Build(Container buildPanel)
        {
            _scrollPanel = new Panel
            {
                CanScroll = true,
                Width = buildPanel.ContentRegion.Width,
                Height = buildPanel.ContentRegion.Height,
                Parent = buildPanel
            };

            _aboutPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Width = _scrollPanel.ContentRegion.Width - LeftPadding - 50,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(LeftPadding, 0),
                Parent = _scrollPanel,
                ControlPadding = new Vector2(0, DefaultSpacing),
                OuterControlPadding = new Vector2(0, DefaultSpacing)
            };

            buildPanel.Resized += OnBuildPanelResized;

            AddSectionHeader(_aboutPanel, "Welcome to Songbook of Tyria");

            AddParagraphWithLinks(_aboutPanel,
                "This songbook is intended for manual music playing, and is directly linked to [OPUS] Divinity's Philharmonic Orchestra's guild ",
                "songbook", "https://www.gw2opus.com/songbook/",
                " and displays our publicly available solo and band tabs.");

            AddSpacer(_aboutPanel, SectionSpacing);
            AddSectionHeader(_aboutPanel, "How to Read Our Notation");

            AddSubHeader(_aboutPanel, "Notes");
            AddCodeBlock(_aboutPanel, "1  2  3  4  5  6  7  8");
            AddParagraph(_aboutPanel,
                "Regular numbers are notes from your skill bar. You can create a separate keybind " +
                "profile for playing music without hindering your ability to play the game.");

            AddCodeBlock(_aboutPanel, "①  ②  ③  ④  ⑤", useLargerFont: true);
            AddParagraph(_aboutPanel,
                "Circled numbers are F1, F2, F3, F4 and F5 keys from piano, which are sharps/black keys on a real piano.");

            AddNote(_aboutPanel,
                "Note: If you are used to different sharps notation, you can directly change these to your preferences.");

            AddSubHeader(_aboutPanel, "Octaves");
            AddCodeBlock(_aboutPanel, "[ 1 2 3 ]   1 2 3   ( 1 2 3 )");
            AddParagraph(_aboutPanel,
                "Square brackets [ ] represent low octave, no brackets is medium octave, and round brackets ( ) are high octave.");


            AddSubHeader(_aboutPanel, "Bars");
            AddCodeBlock(_aboutPanel, "| 1 2 3 4 |");
            AddParagraph(_aboutPanel,
                "Vertical lines on the sides show boundaries of a single bar (4 beats in 4/4 time signature).");

            AddSubHeader(_aboutPanel, "Lines");
            AddCodeBlock(_aboutPanel, "| 1 2 3 4 | 1 2 3 4 | 1 2 3 4 | 1 2 3 4 |");
            AddParagraph(_aboutPanel, "One line consists of several bars.");

            AddSubHeader(_aboutPanel, "Sections");
            AddCodeBlock(_aboutPanel,
                "A\n" +
                "| 1 2 3 4 | 1 2 3 4 | 1 2 3 4 | 1 2 3 4 |\n" +
                "B\n" +
                "| 5 6 7 8 | 5 6 7 8 | 5 6 7 8 | 5 6 7 8 |\n" +
                "C\n" +
                "| (1 2 3 4) | (1 2 3 4) | (1 2 3 4) |");
            AddParagraph(_aboutPanel,
                "Letters (A, B, C, D) represent different parts of the song, such as introduction, verse, chorus, " +
                "and usually just separate groups of lines for better readability. One section consists of several lines of bars.");

            AddSpacer(_aboutPanel, SectionSpacing);
            AddSectionHeader(_aboutPanel, "Notation Symbols & Spacing");

            AddParagraph(_aboutPanel,
                "Often the notation contains more than just 4 simple groups of notes. In this case you would have to adjust " +
                "tempo accordingly. Additionally, there are symbols that represent empty notes, breaks, pauses and arpeggios " +
                "(notes intended to be rolled quickly). Notes are often grouped together and spaced from each other, representing " +
                "the tempo of a song.");

            AddSubHeader(_aboutPanel, "Example");
            AddCodeBlock(_aboutPanel, "| 1 2 -3 45 -6 | 12 3/4/5 – 78 | ~123 -45 – 888 |");

            AddSubHeader(_aboutPanel, "Symbol Reference");

            AddSymbolRow(_aboutPanel, "1/3/5", "Slashes between numbers represent a chord - these notes are meant to be pressed together.");

            AddSymbolRow(_aboutPanel, "[3/5]/3/5/(3/5)", "Multi-octave chord. Roll from low to high as quickly as possible, similar to an arpeggio.");

            AddSymbolRow(_aboutPanel, "-3", "A minus sign followed by a note shows an offbeat note, played later than the beat.");

            AddSymbolRow(_aboutPanel, "–", "An empty note/full beat rest. Example: | 1 – 3 – | shows every other beat being empty.");

            AddSymbolRow(_aboutPanel, "~123", "Arpeggio - three notes rolled quickly together, almost like a chord but slower than a triplet. Also written as 1.2.3 or ~123~");

            AddNote(_aboutPanel, "Some tabs have highlighted notation for easier visibility of triplets and arpeggios.");

            AddSpacer(_aboutPanel, SectionSpacing);
            AddSectionHeader(_aboutPanel, "Need More Help?");

            AddParagraphWithLinks(_aboutPanel,
                "For more tips and a detailed ",
                "'How to Play'", "https://www.gw2opus.com/how-to-play/",
                " guide, you can visit our ",
                "website", "https://www.gw2opus.com/",
                " or feel free to reach out to our guild on Discord.");
        }

        private void OnBuildPanelResized(object sender, ResizedEventArgs e)
        {
            if (_scrollPanel != null && sender is Container container)
            {
                _scrollPanel.Width = container.ContentRegion.Width;
                _scrollPanel.Height = container.ContentRegion.Height;

                if (_aboutPanel != null)
                {
                    _aboutPanel.Width = _scrollPanel.ContentRegion.Width - LeftPadding - 50;
                }
            }
        }

        protected override void Unload()
        {
            if (_scrollPanel?.Parent != null)
            {
                _scrollPanel.Parent.Resized -= OnBuildPanelResized;
            }
            base.Unload();
        }

        private static void AddSectionHeader(Container parent, string text)
        {
            new Label
            {
                Text = text,
                Font = GameService.Content.GetFont(ContentService.FontFace.Menomonia, ContentService.FontSize.Size20, ContentService.FontStyle.Regular),
                TextColor = new Color(255, 200, 100),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = parent
            };
        }

        private static void AddSubHeader(Container parent, string text)
        {
            AddSpacer(parent, 3);
            new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont18,
                TextColor = new Color(180, 220, 255),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = parent
            };
        }

        private static void AddParagraph(Container parent, string text, Color? color = null)
        {
            var label = new Label
            {
                Text = text,
                Font = GameService.Content.DefaultFont16,
                TextColor = color ?? Color.White,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                WrapText = true,
                Width = Math.Max(100, parent.Width - 20),
                Parent = parent
            };
            parent.Resized += (s, e) => { label.Width = Math.Max(100, parent.Width - 20); };
        }

        private static void AddParagraphWithLinks(Container parent, params object[] segments)
        {
            var flowPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                ControlPadding = new Vector2(0, 0),
                Parent = parent
            };

            var font = GameService.Content.DefaultFont16;
            var linkColor = new Color(100, 200, 255);

            int i = 0;
            while (i < segments.Length)
            {
                var text = segments[i] as string;
                if (text != null && i + 2 < segments.Length
                    && segments[i + 1] is string linkText
                    && segments[i + 2] is string url
                    && url.StartsWith("http"))
                {
                    AddInlineWords(flowPanel, text, font, Color.White);
                    AddInlineLink(flowPanel, linkText, font, linkColor, url);
                    i += 3;
                }
                else if (text != null)
                {
                    AddInlineWords(flowPanel, text, font, Color.White);
                    i++;
                }
                else
                {
                    i++;
                }
            }
        }

        private static void AddInlineWords(Container parent, string text, BitmapFont font, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0 && i > 0) continue;

                var word = words[i];
                if (i < words.Length - 1)
                    word += " ";

                new Label
                {
                    Text = word,
                    Font = font,
                    TextColor = color,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = parent
                };
            }
        }

        private static void AddInlineLink(Container parent, string text, BitmapFont font, Color color, string url)
        {
            var link = new Label
            {
                Text = text,
                Font = font,
                TextColor = color,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                BasicTooltipText = url,
                Parent = parent
            };
            link.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Logger.GetLogger<AboutView>().Warn(ex, "Failed to open URL: {Url}", url);
                }
            };
        }

        private static void AddCodeBlock(Container parent, string text, bool useLargerFont = false)
        {
            var codePanel = new Panel
            {
                BackgroundColor = new Color(20, 20, 25),
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                Parent = parent
            };

            var fontSize = useLargerFont ? 30 : 22;
            var font = NotationRenderer.GetFont(fontSize) ?? NotationRenderer.GetFont(22);

            var verticalFlowPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(15, 6),
                Parent = codePanel
            };

            foreach (var line in text.Split('\n'))
            {
                var lineFlowPanel = new FlowPanel
                {
                    FlowDirection = ControlFlowDirection.SingleLeftToRight,
                    HeightSizingMode = SizingMode.AutoSize,
                    WidthSizingMode = SizingMode.Fill,
                    Parent = verticalFlowPanel
                };

                RenderColorizedLine(lineFlowPanel, line, font);
            }
        }

        private static void AddSymbolRow(Container parent, string symbol, string description)
        {
            var rowPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                ControlPadding = new Vector2(10, 0),
                Parent = parent
            };

            var font = NotationRenderer.GetFont(22);

            var symbolPanel = new Panel
            {
                BackgroundColor = new Color(20, 20, 25),
                HeightSizingMode = SizingMode.AutoSize,
                Width = 200,
                Parent = rowPanel
            };

            var symbolFlowPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                OuterControlPadding = new Vector2(10, 4),
                Parent = symbolPanel
            };

            RenderColorizedLine(symbolFlowPanel, symbol, font);

            var descLabel = new Label
            {
                Text = description,
                Font = GameService.Content.DefaultFont16,
                TextColor = Color.White,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                WrapText = true,
                Width = Math.Max(100, parent.Width - 240),
                Parent = rowPanel
            };

            rowPanel.Resized += (s, e) =>
            {
                descLabel.Width = Math.Max(100, rowPanel.Width - 220);
            };
        }

        private static void RenderColorizedLine(Container parent, string line, BitmapFont font)
        {
            var pipeColor = new Color(107, 255, 107);
            var lowOctaveColor = new Color(107, 181, 255);
            var highOctaveColor = new Color(255, 107, 107);
            var defaultColor = new Color(240, 240, 240);

            bool inLowOctave = false;
            bool inHighOctave = false;
            var currentText = new System.Text.StringBuilder();
            var currentColor = defaultColor;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                Color charColor;

                if (c == '|')
                {
                    charColor = pipeColor;
                }
                else if (c == '[')
                {
                    inLowOctave = true;
                    charColor = lowOctaveColor;
                }
                else if (c == ']')
                {
                    charColor = lowOctaveColor;
                    inLowOctave = false;
                }
                else if (c == '(')
                {
                    inHighOctave = true;
                    charColor = highOctaveColor;
                }
                else if (c == ')')
                {
                    charColor = highOctaveColor;
                    inHighOctave = false;
                }
                else if (inLowOctave)
                {
                    charColor = lowOctaveColor;
                }
                else if (inHighOctave)
                {
                    charColor = highOctaveColor;
                }
                else
                {
                    charColor = defaultColor;
                }

                if (charColor != currentColor && currentText.Length > 0)
                {
                    new Label
                    {
                        Text = currentText.ToString(),
                        Font = font,
                        TextColor = currentColor,
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Parent = parent
                    };
                    currentText.Clear();
                }

                currentColor = charColor;
                currentText.Append(c);
            }

            if (currentText.Length > 0)
            {
                new Label
                {
                    Text = currentText.ToString(),
                    Font = font,
                    TextColor = currentColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = parent
                };
            }
        }

        private static void AddNote(Container parent, string text)
        {
            var notePanel = new Panel
            {
                HeightSizingMode = SizingMode.AutoSize,
                WidthSizingMode = SizingMode.Fill,
                Parent = parent
            };

            var noteLabel = new Label
            {
                Text = "*" + text,
                Font = GameService.Content.DefaultFont14,
                TextColor = Color.White,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                WrapText = true,
                Width = Math.Max(100, parent.Width - 100),
                Location = new Point(20, 0),
                Parent = notePanel
            };

            notePanel.Resized += (s, e) =>
            {
                noteLabel.Width = Math.Max(100, notePanel.Width - 40);
            };
        }

        private static void AddSpacer(Container parent, int height)
        {
            new Panel
            {
                Height = height,
                Width = 1,
                Parent = parent
            };
        }
    }
}
