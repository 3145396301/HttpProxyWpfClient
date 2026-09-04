using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

namespace HttpProxyWpfClient.code.Pages.Util
{
    /// <summary>
    /// 在 AvalonEdit TextEditor 中以常驻底色高亮指定的偏移区间（区别于临时的选中态高亮）。
    /// 每个 TextEditor 实例对应一份高亮状态，通过 SetHighlight 更新，传入 length&lt;=0 表示清除高亮。
    /// </summary>
    public class SearchHighlightRenderer : IBackgroundRenderer
    {
        private static readonly Brush HighlightBrush = new SolidColorBrush(Color.FromArgb(160, 255, 200, 0));

        private readonly Dictionary<TextEditor, (int Start, int Length)> _highlights = new();
        private readonly HashSet<TextEditor> _registered = new();

        public KnownLayer Layer => KnownLayer.Selection;

        /// <summary>
        /// 设置（或清除，length&lt;=0 时）某个编辑器当前的高亮区间，并触发重绘
        /// </summary>
        public void SetHighlight(TextEditor editor, int start, int length)
        {
            if (!_registered.Contains(editor))
            {
                editor.TextArea.TextView.BackgroundRenderers.Add(this);
                _registered.Add(editor);
            }

            _highlights[editor] = (start, length);
            editor.TextArea.TextView.InvalidateLayer(Layer);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            TextEditor editor = _highlights.Keys.FirstOrDefault(e => e.TextArea.TextView == textView);
            if (editor == null) return;
            if (!_highlights.TryGetValue(editor, out var range) || range.Length <= 0) return;
            if (textView.Document == null) return;

            int docLength = textView.Document.TextLength;
            int start = Math.Max(0, Math.Min(range.Start, docLength));
            int end = Math.Max(start, Math.Min(range.Start + range.Length, docLength));
            if (start >= end) return;

            textView.EnsureVisualLines();
            var geoBuilder = new BackgroundGeometryBuilder
            {
                CornerRadius = 2,
                AlignToWholePixels = true
            };
            geoBuilder.AddSegment(textView, new ICSharpCode.AvalonEdit.Document.TextSegment
            {
                StartOffset = start,
                EndOffset = end
            });

            Geometry geometry = geoBuilder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(HighlightBrush, null, geometry);
            }
        }
    }
}
