namespace ModsCommon.Trajectory {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// A single intersection point between two trajectories, expressed as a parameter on each.
    /// Ported from CS1 ModsCommon's <c>Intersects.cs</c>.
    /// </summary>
    public readonly struct Intersection : IEquatable<Intersection> {
        public const float MinLength = 0.5f;

        public static ComponentComparer FirstComparer { get; } = new ComponentComparer(true, true);
        public static ComponentComparer SecondComparer { get; } = new ComponentComparer(false, true);
        public static ComponentComparer FirstApproxComparer { get; } = new ComponentComparer(true, false);
        public static ComponentComparer SecondApproxComparer { get; } = new ComponentComparer(false, false);

        public static Intersection NotIntersect => new Intersection(false, default, default);

        public readonly ITrajectory first;
        public readonly ITrajectory second;
        public readonly float firstT;
        public readonly float secondT;
        public readonly bool isIntersect;
        public readonly bool inverted;

        private Intersection(bool isIntersect, float firstT, float secondT, ITrajectory first = default, ITrajectory second = default, bool inverted = false) {
            this.isIntersect = isIntersect;
            this.firstT = firstT;
            this.secondT = secondT;
            this.first = first;
            this.second = second;
            this.inverted = inverted;
        }

        public Intersection(float firstT, float secondT, ITrajectory first = default, ITrajectory second = default) {
            isIntersect = true;
            this.firstT = firstT;
            this.secondT = secondT;
            this.first = first;
            this.second = second;
            inverted = false;
        }

        public Intersection GetReverse() => !isIntersect ? this : new Intersection(isIntersect, secondT, firstT, second, first, !inverted);

        public static Intersection CalculateSingle(ITrajectory firstTrajectory, ITrajectory secondTrajectory) {
            var result = Calculate(firstTrajectory, secondTrajectory);
            return result.Count > 0 ? result[0] : NotIntersect;
        }

        public static bool CalculateSingle(ITrajectory firstTrajectory, ITrajectory secondTrajectory, out float firstT, out float secondT) {
            var result = Calculate(firstTrajectory, secondTrajectory);
            if (result.Count > 0) {
                firstT = result[0].firstT;
                secondT = result[0].secondT;
                return true;
            }

            firstT = 0f;
            secondT = 0f;
            return false;
        }

        public static bool Intersect(ITrajectory firstTrajectory, ITrajectory secondTrajectory) => Calculate(firstTrajectory, secondTrajectory).Any();

        /// <summary>
        /// Finds every point (within [0,1] on both trajectories) where <paramref name="firstTrajectory"/>
        /// crosses <paramref name="secondTrajectory"/>. Straight×straight is solved directly; any
        /// combination involving a curve recursively subdivides both trajectories until each subdivided
        /// piece is straight-enough to solve directly (<see cref="Colossal.Mathematics"/> has no native
        /// curve×curve intersection, so this recursive approach — not a closed-form solve — is required).
        /// </summary>
        public static List<Intersection> Calculate(ITrajectory firstTrajectory, ITrajectory secondTrajectory) {
            var result = new List<Intersection>();

            if (firstTrajectory.TrajectoryType == TrajectoryType.Line) {
                var firstStraight = (StraightTrajectory)firstTrajectory;
                if (secondTrajectory.TrajectoryType == TrajectoryType.Line) {
                    IntersectStraightWithStraight(result, in firstStraight, (StraightTrajectory)secondTrajectory);
                } else {
                    IntersectITrajectoryWithStraight(result, in firstStraight, secondTrajectory, false, SplitData.Default);
                }
            } else if (secondTrajectory.TrajectoryType == TrajectoryType.Line) {
                var secondStraight = (StraightTrajectory)secondTrajectory;
                IntersectITrajectoryWithStraight(result, in secondStraight, firstTrajectory, true, SplitData.Default);
            } else {
                IntersectITrajectoryWithITrajectory(result, firstTrajectory, secondTrajectory, SplitData.Default, SplitData.Default);
            }

            return result;
        }

        public static List<Intersection> Calculate(ITrajectory trajectory, IEnumerable<ITrajectory> otherTrajectories, bool onlyIntersect = false)
            => otherTrajectories.SelectMany(t => Calculate(trajectory, t)).Where(i => !onlyIntersect || i.isIntersect).ToList();

        #region STRAIGHT - STRAIGHT

        public static void IntersectStraightWithStraight(List<Intersection> results, in StraightTrajectory first, in StraightTrajectory second) {
            if (MathUtils.Intersect(new Line2(first.StartPosition.xz, first.EndPosition.xz), new Line2(second.StartPosition.xz, second.EndPosition.xz), out var t)) {
                if (IsCorrectT(in first, t.x) && IsCorrectT(in second, t.y)) {
                    results.Add(new Intersection(t.x, t.y, first, second));
                }
            }
        }

        public static Intersection GetIntersection(in StraightTrajectory first, in StraightTrajectory second) {
            if (MathUtils.Intersect(new Line2(first.StartPosition.xz, first.EndPosition.xz), new Line2(second.StartPosition.xz, second.EndPosition.xz), out var t)
                && IsCorrectT(in first, t.x) && IsCorrectT(in second, t.y)) {
                return new Intersection(t.x, t.y, first, second);
            }

            return NotIntersect;
        }

        #endregion

        #region ITRAJECTORY - ITRAJECTORY

        private static IntersectResult IntersectITrajectoryWithITrajectory(List<Intersection> results, ITrajectory first, ITrajectory second, SplitData firstData, SplitData secondData) {
            var firstPoints = CalcTrajectoryParts(first, firstData, out var firstParts);
            var secondPoints = CalcTrajectoryParts(second, secondData, out var secondParts);
            var result = new IntersectResult();

            if (firstParts == 1 && secondParts == 1) {
                if (IntersectSections(firstPoints[0].pos, firstPoints[1].pos, secondPoints[0].pos, secondPoints[1].pos, out var firstT, out var secondT)) {
                    firstT = 1f / firstData.total * (firstData.index + firstData.merge * firstT);
                    secondT = 1f / secondData.total * (secondData.index + secondData.merge * secondT);
                    results.Add(new Intersection(firstT, secondT, first, second));
                    return IntersectResult.Positive;
                }

                result.Add(firstT, secondT);
            } else {
                for (var i = 0; i < firstParts; i += 1) {
                    for (var j = 0; j < secondParts; j += 1) {
                        if (IntersectSections(firstPoints[i].pos, firstPoints[i + 1].pos, secondPoints[j].pos, secondPoints[j + 1].pos, out var firstT, out var secondT)) {
                            var nextFirstData = GetNext(firstData, i, firstParts, firstT);
                            var nextSecondData = GetNext(secondData, j, secondParts, secondT);

                            var nextResult = IntersectITrajectoryWithITrajectory(results, first, second, nextFirstData, nextSecondData);
                            if (nextResult.Intersect) {
                                return IntersectResult.Positive;
                            }

                            var needI = NeedCheck(nextResult.firstDir, i, firstParts, out var nextI);
                            var needJ = NeedCheck(nextResult.secondDir, j, secondParts, out var nextJ);
                            if (!needI && !needJ) {
                                continue;
                            }

                            nextFirstData = GetNext(firstData, nextI, firstParts, firstT);
                            nextSecondData = GetNext(secondData, nextJ, secondParts, secondT);

                            nextResult = IntersectITrajectoryWithITrajectory(results, first, second, nextFirstData, nextSecondData);
                            if (nextResult.Intersect) {
                                return IntersectResult.Positive;
                            }
                        }

                        // Sibling of the `if` above, not an `else` — matches CS1's fall-through semantics:
                        // this must also run after a taken-but-non-positive branch above (unless `continue`
                        // was hit), so the caller's own NeedCheck sees this pair's before/after direction.
                        result.Add(firstT, secondT);
                    }
                }
            }

            return result;

            static SplitData GetNext(SplitData data, int i, int count, float t) {
                if (count == 1) {
                    return data;
                }

                if (t < 0.1f && i > 0) {
                    return new SplitData((data.index * count / data.merge + i) * 2 - 1, data.total * count / data.merge * 2, 2);
                }

                if (t > 0.9f && i < count - 1) {
                    return new SplitData((data.index * count / data.merge + i) * 2 + 1, data.total * count / data.merge * 2, 2);
                }

                return new SplitData(data.index * count / data.merge + i, data.total * count / data.merge, 1);
            }

            static bool IntersectSections(float3 a, float3 b, float3 c, float3 d, out float p, out float q) {
                if (MathUtils.Intersect(new Line2(a.xz, b.xz), new Line2(c.xz, d.xz), out var t) && IsCorrectT(t.x) && IsCorrectT(t.y)) {
                    var dot = math.dot(math.normalizesafe(b.xz - a.xz), math.normalizesafe(d.xz - c.xz));
                    if (math.abs(math.abs(dot) - 1f) > 1e-4f) {
                        p = t.x;
                        q = t.y;
                        return true;
                    }
                }

                p = 0f;
                q = 0f;
                return false;
            }

            static bool NeedCheck(IntersectionDirection dir, int i, int count, out int nextI) {
                if (dir == IntersectionDirection.Before && i > 0) {
                    nextI = i - 1;
                    return true;
                }

                if (dir == IntersectionDirection.After && i < count - 1) {
                    nextI = i + 1;
                    return true;
                }

                nextI = i;
                return false;
            }
        }

        private readonly struct SplitData {
            public static SplitData Default = new SplitData(0, 1, 1);

            public readonly int index;
            public readonly int total;
            public readonly int merge;

            public SplitData(int index, int total, int merge = 1) {
                this.index = index;
                this.total = total;
                this.merge = merge;
            }
        }

        private struct IntersectResult {
            public static IntersectResult Positive = new IntersectResult { firstDir = IntersectionDirection.Middle, secondDir = IntersectionDirection.Middle };

            public IntersectionDirection firstDir;
            public IntersectionDirection secondDir;

            public bool Intersect => firstDir == IntersectionDirection.Middle && secondDir == IntersectionDirection.Middle;

            public void Add(float firstT, float secondT) {
                firstDir |= GetDirection(firstT);
                secondDir |= GetDirection(secondT);
            }

            private static IntersectionDirection GetDirection(float t) {
                if (t < 0f) {
                    return IntersectionDirection.Before;
                }

                return t > 1f ? IntersectionDirection.After : IntersectionDirection.Middle;
            }
        }

        [Flags]
        private enum IntersectionDirection {
            None = 0,
            Before = 1,
            Middle = 2,
            After = 4
        }

        #endregion

        #region ITRAJECTORY - STRAIGHT

        private static void IntersectITrajectoryWithStraight(List<Intersection> results, in StraightTrajectory line, ITrajectory trajectory, bool invert, SplitData data) {
            var points = CalcTrajectoryParts(trajectory, data, out var parts);

            if (parts > 1) {
                for (var i = 0; i < parts; i += 1) {
                    if (IntersectSectionAndRay(in line, points[i].pos, points[i + 1].pos, out _, out _)) {
                        var nextData = new SplitData(data.index * parts + i, data.total * parts);
                        IntersectITrajectoryWithStraight(results, in line, trajectory, invert, nextData);
                    }
                }
            } else if (parts == 1 && IntersectSectionAndRay(in line, points[0].pos, points[1].pos, out var firstT, out var secondT)) {
                secondT = 1f / data.total * (data.index + secondT);
                results.Add(!invert ? new Intersection(firstT, secondT, line, trajectory) : new Intersection(secondT, firstT, trajectory, line));
            }

            static bool IntersectSectionAndRay(in StraightTrajectory line, float3 start, float3 end, out float p, out float q) {
                if (MathUtils.Intersect(new Line2(line.StartPosition.xz, line.EndPosition.xz), new Line2(start.xz, end.xz), out var t)
                    && IsCorrectT(in line, t.x) && IsCorrectT(t.y)) {
                    p = t.x;
                    q = t.y;
                    return true;
                }

                p = 0f;
                q = 0f;
                return false;
            }
        }

        #endregion

        private static TrajectoryPoint[] CalcTrajectoryParts(ITrajectory trajectory, SplitData data, out int parts) {
            var startT = 1f / data.total * data.index;
            var endT = 1f / data.total * (data.index + data.merge);

            var start = trajectory.Position(startT);
            var end = trajectory.Position(endT);
            var middle = trajectory.Position((startT + endT) * 0.5f);

            var length = math.max(math.distance(middle, start) + math.distance(end, middle), 0f);
            parts = math.min((int)math.ceil(length / MinLength), 10);
            if (parts > data.merge) {
                parts = parts / data.merge * data.merge;
            }

            parts = math.max(parts, 1);

            var points = new TrajectoryPoint[parts + 1];
            points[0] = new TrajectoryPoint(start, startT);
            points[parts] = new TrajectoryPoint(end, endT);

            for (var i = 1; i < parts; i += 1) {
                var t = startT + 1f / (parts * data.total / data.merge) * i;
                points[i] = new TrajectoryPoint(trajectory.Position(t), t);
            }

            return points;
        }

        public static bool IsCorrectT(float t) => t is >= 0f and <= 1f;
        public static bool IsCorrectT(in StraightTrajectory line, float t) => (line.StartLimited ? 0f : float.MinValue) <= t && t <= (line.EndLimited ? 1f : float.MaxValue);

        /// <remarks>
        /// CS1 ModsCommon also had bounding-box quick-reject overloads of <c>CanIntersect</c> (against a
        /// <c>Rect</c>, a point array, or a bezier's control hull) used by its <c>Contour</c>
        /// class to skip full intersection tests for trajectories that obviously can't cross. Deferred to
        /// the domain-model phase, where a real contour/bounding type exists to test against — the core
        /// <see cref="Calculate(ITrajectory, ITrajectory)"/> algorithm above works correctly without it,
        /// just without that early-out optimization.
        /// </remarks>
        public static Side GetSide(float posX, float posZ, float dirX, float dirZ, float pointX, float pointZ)
            => dirX * (pointX - posX) + dirZ * (pointZ - posZ) >= 0f ? Side.Right : Side.Left;

        public static Side GetSide(float3 direction, float3 toCheck) => direction.z * toCheck.x - direction.x * toCheck.z >= 0f ? Side.Right : Side.Left;

        public override string ToString() => isIntersect ? $"{firstT:0.###} - {secondT:0.###}" : "Not intersect";

        public bool Equals(Intersection other) => firstT == other.firstT && secondT == other.secondT;
        public override bool Equals(object obj) => obj is Intersection other && Equals(other);
        public override int GetHashCode() => firstT.GetHashCode() ^ secondT.GetHashCode();
        public static bool operator ==(Intersection a, Intersection b) => a.Equals(b);
        public static bool operator !=(Intersection a, Intersection b) => !a.Equals(b);

        public enum Side {
            Left,
            Right
        }

        public class ComponentComparer : IComparer<Intersection> {
            private readonly bool m_IsFirst;
            private readonly bool m_Strict;

            public ComponentComparer(bool isFirst, bool strict) {
                m_IsFirst = isFirst;
                m_Strict = strict;
            }

            public int Compare(Intersection x, Intersection y) {
                return m_IsFirst
                    ? (!m_Strict && Approximately(x.firstT, y.firstT) ? 0 : x.firstT.CompareTo(y.firstT))
                    : (!m_Strict && Approximately(x.secondT, y.secondT) ? 0 : x.secondT.CompareTo(y.secondT));
            }

            private static bool Approximately(float a, float b) => math.abs(b - a) < 0.001f * math.max(math.abs(a), math.abs(b));
        }

        private readonly struct TrajectoryPoint {
            public readonly float3 pos;
            public readonly float t;

            public TrajectoryPoint(float3 pos, float t) {
                this.pos = pos;
                this.t = t;
            }
        }
    }

    /// <summary>A pair of intersections bounding a piece of a trajectory between two crossing points.</summary>
    public struct IntersectionPair {
        public Intersection from;
        public Intersection to;

        public bool Inverted { get; private set; }
        public IntersectionPair Reverse => new IntersectionPair(to, from) { Inverted = !Inverted };

        public IntersectionPair(Intersection from, Intersection to) {
            this.from = from;
            this.to = to;
            Inverted = false;
        }

        public bool Contain(Intersection intersection) => from == intersection || to == intersection;

        public Intersection GetOther(Intersection intersection) {
            if (intersection == from) {
                return to;
            }

            return intersection == to ? from : Intersection.NotIntersect;
        }

        public override string ToString() => $"{from.secondT:0.###} - [{from.firstT:0.###} - {to.firstT:0.###}] - {to.secondT:0.###}";
    }
}
