using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    public enum GameIconKind : byte
    {
        Gem,
        Clock,
        Play,
        Restart,
        Quit,
        Walk,
        Run,
        FoxMark,
    }

    /// <summary>
    /// Flat vector icon drawn with <see cref="Painter2D"/> in the largest centered square of the
    /// element. Monochrome icons use the element's USS <c>color</c>; the gem keeps its gold faces.
    /// </summary>
    [UxmlElement]
    public partial class GameIcon : VisualElement
    {
        private const float StrokeWidth = 0.1f;

        private static readonly Color GemTop = new(1f, 0.86f, 0.36f);
        private static readonly Color GemLeft = new(0.97f, 0.75f, 0.2f);
        private static readonly Color GemRight = new(0.84f, 0.58f, 0.1f);

        // Traced from the app icon's fox mark, normalized to its bounding box.
        private static readonly Vector2[] FoxOutline =
        {
            new(0.109f, 0f), new(0.393f, 0.295f), new(0.608f, 0.295f), new(0.891f, 0f),
            new(0.931f, 0.362f), new(0.846f, 0.477f), new(1f, 0.64f), new(0.780f, 0.812f),
            new(0.699f, 0.812f), new(0.499f, 1f), new(0.306f, 0.813f), new(0.221f, 0.812f),
            new(0f, 0.64f), new(0.153f, 0.477f), new(0.068f, 0.362f),
        };

        private GameIconKind _kind;

        public GameIcon()
        {
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        [UxmlAttribute]
        public GameIconKind Kind
        {
            get => _kind;
            set
            {
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            var rect = contentRect;
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f)
            {
                return;
            }

            var origin = rect.center - Vector2.one * (size * 0.5f);
            var painter = context.painter2D;
            var color = resolvedStyle.color;
            painter.strokeColor = color;
            painter.fillColor = color;
            painter.lineWidth = StrokeWidth * size;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            Vector2 P(float x, float y) => origin + new Vector2(x, y) * size;

            switch (_kind)
            {
                case GameIconKind.Gem:
                    FillPolygon(painter, GemTop, P(0.5f, 0.06f), P(0.9f, 0.29f), P(0.5f, 0.52f), P(0.1f, 0.29f));
                    FillPolygon(painter, GemLeft, P(0.1f, 0.29f), P(0.5f, 0.52f), P(0.5f, 0.96f), P(0.1f, 0.73f));
                    FillPolygon(painter, GemRight, P(0.5f, 0.52f), P(0.9f, 0.29f), P(0.9f, 0.73f), P(0.5f, 0.96f));
                    break;
                case GameIconKind.Clock:
                    painter.BeginPath();
                    painter.Arc(P(0.5f, 0.5f), 0.4f * size, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.Stroke();
                    StrokePolyline(painter, P(0.5f, 0.26f), P(0.5f, 0.5f), P(0.67f, 0.6f));
                    break;
                case GameIconKind.Play:
                    FillPolygon(painter, color, P(0.24f, 0.14f), P(0.86f, 0.5f), P(0.24f, 0.86f));
                    break;
                case GameIconKind.Restart:
                    DrawRestart(painter, P(0.5f, 0.52f), 0.34f * size, size);
                    break;
                case GameIconKind.Quit:
                    StrokePolyline(painter, P(0.46f, 0.14f), P(0.14f, 0.14f), P(0.14f, 0.86f), P(0.46f, 0.86f));
                    StrokePolyline(painter, P(0.4f, 0.5f), P(0.86f, 0.5f));
                    StrokePolyline(painter, P(0.7f, 0.34f), P(0.86f, 0.5f), P(0.7f, 0.66f));
                    break;
                case GameIconKind.Walk:
                    StrokePolyline(painter, P(0.36f, 0.18f), P(0.68f, 0.5f), P(0.36f, 0.82f));
                    break;
                case GameIconKind.Run:
                    StrokePolyline(painter, P(0.16f, 0.18f), P(0.48f, 0.5f), P(0.16f, 0.82f));
                    StrokePolyline(painter, P(0.52f, 0.18f), P(0.84f, 0.5f), P(0.52f, 0.82f));
                    break;
                case GameIconKind.FoxMark:
                    DrawFoxMark(painter, origin, size);
                    break;
            }
        }

        private static void DrawRestart(Painter2D painter, Vector2 center, float radius, float size)
        {
            const float StartDegrees = 20f;
            const float EndDegrees = 300f;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(StartDegrees), Angle.Degrees(EndDegrees));
            painter.Stroke();

            var end = EndDegrees * Mathf.Deg2Rad;
            var radial = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
            var tangent = new Vector2(-radial.y, radial.x);
            var tip = center + radial * radius;
            FillPolygon(painter, painter.strokeColor,
                tip + tangent * (0.16f * size),
                tip + radial * (0.13f * size),
                tip - radial * (0.13f * size));
        }

        /// <summary>Fox head outline with two square eyes cut out (odd-even fill).</summary>
        private static void DrawFoxMark(Painter2D painter, Vector2 origin, float size)
        {
            const float Height = 0.955f;
            var offset = origin + new Vector2(0f, (1f - Height) * 0.5f * size);
            Vector2 F(float x, float y) => offset + new Vector2(x, y * Height) * size;

            painter.BeginPath();
            painter.MoveTo(F(FoxOutline[0].x, FoxOutline[0].y));
            for (var i = 1; i < FoxOutline.Length; i++)
            {
                painter.LineTo(F(FoxOutline[i].x, FoxOutline[i].y));
            }

            painter.ClosePath();
            AddSquare(painter, F(0.281f, 0.577f), F(0.376f, 0.676f));
            AddSquare(painter, F(0.624f, 0.577f), F(0.719f, 0.676f));
            painter.Fill(FillRule.OddEven);
        }

        private static void AddSquare(Painter2D painter, Vector2 min, Vector2 max)
        {
            painter.MoveTo(min);
            painter.LineTo(new Vector2(max.x, min.y));
            painter.LineTo(max);
            painter.LineTo(new Vector2(min.x, max.y));
            painter.ClosePath();
        }

        private static void FillPolygon(Painter2D painter, Color color, params Vector2[] points)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                painter.LineTo(points[i]);
            }

            painter.ClosePath();
            painter.Fill();
        }

        private static void StrokePolyline(Painter2D painter, params Vector2[] points)
        {
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                painter.LineTo(points[i]);
            }

            painter.Stroke();
        }
    }
}
