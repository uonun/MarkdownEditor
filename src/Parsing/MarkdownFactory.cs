﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Markdig;
using Markdig.Extensions.Footers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace MarkdownEditor.Parsing
{
    public static class MarkdownFactory
    {
        private static readonly ConditionalWeakTable<ITextSnapshot, MarkdownDocument> CachedDocuments = new ConditionalWeakTable<ITextSnapshot, MarkdownDocument>();

        static MarkdownFactory()
        {
            Pipeline = new MarkdownPipelineBuilder()
                .UsePragmaLines()
                .UseAdvancedExtensions().Build();
        }

        public static MarkdownPipeline Pipeline { get; }


        public static MarkdownDocument ParseToMarkdown(this ITextSnapshot snapshot)
        {
            return CachedDocuments.GetValue(snapshot, key =>
            {
                var text = key.GetText();
                var markdownDocument =  Markdown.Parse(text, Pipeline);
                return markdownDocument;
            });
        }


        public static bool MatchSmartBlock(string text)
        {
            MarkdownDocument doc;
            return MatchSmartBlock(text, out doc);
        }

        public static bool MatchSmartBlock(string text, out MarkdownDocument doc)
        {
            // Temp workaround for list items: We trim the beginning of the line to be able to parse nested list items.
            text = text.TrimStart();
            doc = Markdown.Parse(text, Pipeline);
            return doc.Count != 0 && (doc[0] is QuoteBlock || doc[0] is ListBlock || (doc[0] is CodeBlock && !(doc[0] is FencedCodeBlock)) || doc[0] is FooterBlock);
        }

        public static bool TryParsePendingSmartBlock(this ITextView view, out List<Block> blocks, out MarkdownDocument doc)
        {
            // Prematch the current line to detect a smart block
            var extend = view.Caret.ContainingTextViewLine.Extent;
            var text = extend.GetText();
            blocks = null;
            doc = null;

            if (!MatchSmartBlock(text))
            {
                return false;
            }

            // Parse only until the end of the line after the caret
            // Because most of the time, a user would have typed characters before typing return
            // it is not efficient to re-use cached MarkdownDocument from Markdownfactory, as it may be invalid,
            // and then after the hit return, the browser would have to be updated with a new snapshot
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var textFromTop = snapshot.GetText(0, view.Caret.ContainingTextViewLine.Extent.Span.End);
            doc = Markdown.Parse(textFromTop, Pipeline);
            var caretPosition = view.Caret.Position.BufferPosition.Position;
            var lastChild = doc.FindBlockAtPosition(caretPosition);
            if (lastChild == null || !lastChild.ContainsPosition(caretPosition))
            {
                return false;
            }

            // Re-build list of blocks
            blocks = new List<Block>();
            var block = lastChild;
            while (block != null)
            {
                // We don't add ListBlock (as they should have list items)
                if (block != doc && !(block is ListBlock))
                {
                    blocks.Add(block);
                }
                block = block.Parent;
            }
            blocks.Reverse();

            return blocks.Count > 0;
        }
    }
}