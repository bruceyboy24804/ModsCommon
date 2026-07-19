namespace ModsCommon.Geometry {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using ModsCommon.Trajectory;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Indices of a single triangle into an external vertex array — the output unit of
    /// <see cref="Triangulator.TriangulateSimple(System.Collections.Generic.IEnumerable{ITrajectory}, out float3[], float, float, float)"/>.
    /// </summary>
    /// <remarks>
    /// CS1 ModsCommon defined this alongside a <c>Polygon</c>/<c>Area</c>/<c>Side</c>/<c>Vertex3</c> set
    /// (in <c>Area.cs</c>) that merges adjacent triangles into height-coherent areas for 3D filler
    /// rendering. That merging step is deferred (see the Phase 1 plan) since nothing in this foundation
    /// phase consumes it yet — only the plain triangle indices are needed for now.
    /// </remarks>
    public readonly struct Triangle {
        public readonly int a;
        public readonly int b;
        public readonly int c;

        public Triangle(int a, int b, int c) {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public IEnumerable<int> GetVertices(WindingDirection direction) {
            if (direction == WindingDirection.ClockWise) {
                yield return c;
                yield return b;
                yield return a;
            } else {
                yield return a;
                yield return b;
                yield return c;
            }
        }

        public override string ToString() => $"{a}-{b}-{c}";
    }

    /// <summary>
    /// Ear-clipping triangulator for a closed contour of trajectories (fillers, crosswalks, any
    /// solid-fill marking shape). Curved edges are first subdivided into near-straight segments (since
    /// ear-clipping operates on straight-edged polygons), then triangulated. Ported from CS1 ModsCommon's
    /// <c>Triangulator.cs</c>.
    /// </summary>
    public class Triangulator {
        public static int[] TriangulateSimple(IEnumerable<ITrajectory> trajectories, out float3[] points, float minAngle = 10f, float minLength = 1f, float maxLength = 50f) {
            var contour = trajectories.ToArray();
            var split = new List<ITrajectory>();
            foreach (var trajectory in contour) {
                SplitTrajectory(0, trajectory, trajectory.DeltaAngle, minAngle, minLength, maxLength, split);
            }

            var direction = contour.GetDirection();
            points = split.Select(t => t.StartPosition).ToArray();
            return TriangulateSimple(points, direction);
        }

        private static void SplitTrajectory(int depth, ITrajectory trajectory, float deltaAngle, float minAngle, float minLength, float maxLength, List<ITrajectory> result) {
            var length = trajectory.Magnitude;
            var needDivide = (deltaAngle > minAngle && length >= minLength) || length > maxLength;

            if (depth < 5 && (needDivide || depth == 0)) {
                trajectory.Divide(out var first, out var second);
                var firstDeltaAngle = first.DeltaAngle;
                var secondDeltaAngle = second.DeltaAngle;

                if (needDivide || deltaAngle > minAngle || firstDeltaAngle + secondDeltaAngle > minAngle) {
                    SplitTrajectory(depth + 1, first, firstDeltaAngle, minAngle, minLength, maxLength, result);
                    SplitTrajectory(depth + 1, second, secondDeltaAngle, minAngle, minLength, maxLength, result);
                    return;
                }
            }

            result.Add(trajectory);
        }

        public static int[] TriangulateSimple(IEnumerable<float3> points, WindingDirection direction) {
            var triangulator = new Triangulator(points, direction);
            var triangles = triangulator.TriangulateSimple();
            return triangles.SelectMany(t => t.GetVertices(direction)).ToArray();
        }

        private WindingDirection Direction { get; }
        private LinkedList<Vertex> Vertices { get; }
        private Dictionary<int, LinkedListNode<Vertex>> Ears { get; } = new Dictionary<int, LinkedListNode<Vertex>>();

        private Triangulator(IEnumerable<float3> points, WindingDirection direction) {
            Vertices = new LinkedList<Vertex>(points.Select((p, i) => new Vertex(p, i)));
            Direction = direction;
        }

        private List<Triangle> TriangulateSimple() {
            foreach (var vertex in EnumerateVertex()) {
                SetConvex(vertex);
                SetHeight(vertex);
                SetEar(vertex);
            }

            var triangles = new List<Triangle>();

            while (Ears.Count != 0) {
                LinkedListNode<Vertex> vertex = null;
                var minH = float.MaxValue;
                foreach (var ear in Ears.Values) {
                    if (ear.Value.deltaH < minH) {
                        vertex = ear;
                        minH = ear.Value.deltaH;
                    }
                }

                var prev = Previous(vertex);
                var next = Next(vertex);

                triangles.Add(new Triangle(next.Value.index, vertex.Value.index, prev.Value.index));
                Ears.Remove(vertex.Value.index);
                Vertices.Remove(vertex);

                if (Vertices.Count < 3) {
                    break;
                }

                SetConvex(prev);
                SetConvex(next);
                SetHeight(prev);
                SetHeight(next);

                SetEar(prev);
                SetEar(next);
            }

            return triangles;
        }

        private void SetConvex(LinkedListNode<Vertex> vertex) {
            if (!vertex.Value.isConvex) {
                vertex.Value = vertex.Value.SetConvex(Previous(vertex).Value, Next(vertex).Value, Direction);
            }
        }

        private void SetHeight(LinkedListNode<Vertex> vertex) => vertex.Value = vertex.Value.SetHeight(Previous(vertex).Value, Next(vertex).Value);

        private void SetEar(LinkedListNode<Vertex> vertex) {
            var prev = Previous(vertex);
            var next = Next(vertex);

            if (vertex.Value.isConvex) {
                if (!EnumerateVertex(next, prev).Any(p => PointInTriangle(prev.Value.position, vertex.Value.position, next.Value.position, p.Value.position))) {
                    Ears[vertex.Value.index] = vertex;
                    return;
                }
            }

            Ears.Remove(vertex.Value.index);
        }

        private static LinkedListNode<Vertex> Next(LinkedListNode<Vertex> node) => node.Next ?? node.List.First;
        private static LinkedListNode<Vertex> Previous(LinkedListNode<Vertex> node) => node.Previous ?? node.List.Last;

        private IEnumerable<LinkedListNode<Vertex>> EnumerateVertex() => EnumerateVertex(Vertices.First);

        private IEnumerable<LinkedListNode<Vertex>> EnumerateVertex(LinkedListNode<Vertex> startFrom) {
            if (startFrom != null) {
                yield return startFrom;
            }

            for (var vertex = Next(startFrom); vertex != startFrom; vertex = Next(vertex)) {
                yield return vertex;
            }
        }

        private IEnumerable<LinkedListNode<Vertex>> EnumerateVertex(LinkedListNode<Vertex> from, LinkedListNode<Vertex> to) {
            for (var vertex = Next(from); vertex != to; vertex = Next(vertex)) {
                yield return vertex;
            }
        }

        private static bool PointInTriangle(float3 a, float3 b, float3 c, float3 p) {
            var area = 0.5f * (-b.z * c.x + a.z * (-b.x + c.x) + a.x * (b.z - c.z) + b.x * c.z);
            var s = 1f / (2f * area) * (a.z * c.x - a.x * c.z + (c.z - a.z) * p.x + (a.x - c.x) * p.z);
            var t = 1f / (2f * area) * (a.x * b.z - a.z * b.x + (a.z - b.z) * p.x + (b.x - a.x) * p.z);
            return s >= 0f && t >= 0f && s + t <= 1f;
        }

        private readonly struct Vertex {
            public readonly float3 position;
            public readonly int index;
            public readonly bool isConvex;
            public readonly float deltaH;

            private Vertex(float3 position, int index, bool isConvex, float deltaH) {
                this.position = position;
                this.index = index;
                this.isConvex = isConvex;
                this.deltaH = deltaH;
            }

            public Vertex(float3 position, int index) : this(position, index, default, default) { }

            public Vertex SetConvex(Vertex prev, Vertex next, WindingDirection direction) {
                var a = position - prev.position;
                var b = next.position - position;

                var sign = (int)math.sign(a.x * b.z - a.z * b.x);
                var isConvex = sign >= 0 ^ direction == WindingDirection.ClockWise;

                return new Vertex(position, index, isConvex, deltaH);
            }

            public Vertex SetHeight(Vertex prev, Vertex next) {
                var min = math.min(position.y, math.min(prev.position.y, next.position.y));
                var max = math.max(position.y, math.max(prev.position.y, next.position.y));
                return new Vertex(position, index, isConvex, max - min);
            }

            public override string ToString() => $"{index}:{position} ({(isConvex ? "Convex" : "Reflex")})";
        }
    }
}
