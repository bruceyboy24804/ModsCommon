namespace ModsCommon.Trajectory {
    #region Using Statements

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// An <see cref="ITrajectory"/> made of multiple trajectories chained end-to-end (each one's
    /// <see cref="ITrajectory.EndPosition"/> must match the next one's <see cref="ITrajectory.StartPosition"/>).
    /// Used to represent a filler/crosswalk contour edge or any other marking edge that isn't a single
    /// straight line or bezier. Ported from CS1 ModsCommon's <c>Combined.cs</c> — pure control-flow, no
    /// engine-specific math.
    /// </summary>
    public struct CombinedTrajectory : ITrajectory, IEnumerable<ITrajectory> {
        public TrajectoryType TrajectoryType => TrajectoryType.Combined;
        private ITrajectory[] Trajectories { get; }

        private float? m_Length;
        private float[] m_Parts;

        public int Count => Trajectories.Length;
        public float Length => m_Length ??= Trajectories.Sum(t => t.Length);

        public float[] Parts {
            get {
                var parts = m_Parts;
                if (parts == null) {
                    var totalLength = Length;
                    parts = new float[Trajectories.Length];

                    var sum = 0f;
                    for (var i = 0; i < parts.Length; i += 1) {
                        parts[i] = sum;
                        sum += Trajectories[i].Length / totalLength;
                    }

                    m_Parts = parts;
                }

                return parts;
            }
        }

        public ITrajectory this[int i] => Trajectories[i];

        public float Magnitude { get; }
        public float DeltaAngle { get; }
        public float3 Direction { get; }
        public float3 StartDirection { get; }
        public float3 EndDirection { get; }
        public float3 StartPosition => Trajectories[0].StartPosition;
        public float3 EndPosition => Trajectories[Trajectories.Length - 1].EndPosition;
        public bool IsZero => Trajectories.All(t => t.IsZero);

        private CombinedTrajectory(ITrajectory[] trajectories, float? length, float[] parts, float magnitude, float deltaAngle, float3 direction, float3 startDirection, float3 endDirection) {
            Trajectories = trajectories;
            m_Length = length;
            m_Parts = parts;
            Magnitude = magnitude;
            DeltaAngle = deltaAngle;
            Direction = direction;
            StartDirection = startDirection;
            EndDirection = endDirection;
        }

        public CombinedTrajectory(params ITrajectory[] trajectories) {
            Trajectories = trajectories;

            if (Trajectories.Length == 0) {
                throw new ArgumentException("Trajectories are empty", nameof(trajectories));
            }

            for (var i = 1; i < Trajectories.Length; i += 1) {
                if (math.distance(Trajectories[i - 1].EndPosition, Trajectories[i].StartPosition) > 0.01f) {
                    throw new ArgumentException($"Trajectories should connect each other. trajectories {i - 1} and {i} are not connected", nameof(trajectories));
                }
            }

            m_Length = null;
            m_Parts = null;

            var first = Trajectories[0];
            var last = Trajectories[Trajectories.Length - 1];
            Magnitude = math.distance(last.EndPosition, first.StartPosition);
            DeltaAngle = 180f - TrajectoryExtensions.AngleDegrees(first.StartDirection, last.EndDirection);
            Direction = math.normalizesafe(last.EndPosition - first.StartPosition);
            StartDirection = first.StartDirection;
            EndDirection = last.EndDirection;
        }

        public CombinedTrajectory(IEnumerable<ITrajectory> trajectories) : this(trajectories.ToArray()) { }

        public IEnumerator<ITrajectory> GetEnumerator() => ((IEnumerable<ITrajectory>)Trajectories).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Trajectories.GetEnumerator();

        private int GetIndex(float t) {
            var parts = Parts;
            for (var i = Count - 1; i >= 0; i -= 1) {
                if (t >= parts[i]) {
                    return i;
                }
            }

            return 0;
        }

        public void GetBounds(int i, out float t0, out float t1) {
            t0 = Parts[i];
            t1 = i < Count - 1 ? Parts[i + 1] : 1f;
        }

        public float GetRelativeLength(int i) => Trajectories[i].Length / Length;

        public float ToPartT(float t, out int i) {
            i = GetIndex(t);
            return ToPartT(i, t);
        }

        public float ToPartT(int i, float t) => math.saturate(t - Parts[i]) / GetRelativeLength(i);

        public float FromPartT(int i, float t) => math.saturate(t * GetRelativeLength(i) + Parts[i]);

        public ITrajectory Cut(float t0, float t1) {
            var minT = ToPartT(math.min(t0, t1), out var startI);
            var maxT = ToPartT(math.max(t0, t1), out var endI);

            if (startI == endI) {
                return t0 <= t1 ? Trajectories[startI].Cut(minT, maxT) : Trajectories[startI].Cut(maxT, minT);
            }

            var trajectories = new List<ITrajectory>();

            if (t0 <= t1) {
                trajectories.Add(Trajectories[startI].Cut(minT, 1f));
                for (var i = startI + 1; i < endI; i += 1) {
                    trajectories.Add(Trajectories[i]);
                }

                trajectories.Add(Trajectories[endI].Cut(0f, maxT));
            } else {
                trajectories.Add(Trajectories[endI].Cut(maxT, 0f));
                for (var i = endI - 1; i > startI; i -= 1) {
                    trajectories.Add(Trajectories[i].Invert());
                }

                trajectories.Add(Trajectories[startI].Cut(1f, minT));
            }

            return new CombinedTrajectory(trajectories);
        }

        public void Divide(out ITrajectory trajectory1, out ITrajectory trajectory2) {
            var firstHalf = new List<ITrajectory>();
            var secondHalf = new List<ITrajectory>();

            for (var i = 0; i < Count; i += 1) {
                GetBounds(i, out var t0, out var t1);

                if (t1 <= 0.5f) {
                    firstHalf.Add(Trajectories[i]);
                } else if (t0 >= 0.5f) {
                    secondHalf.Add(Trajectories[i]);
                } else {
                    var t = (0.5f - t0) / GetRelativeLength(i);
                    firstHalf.Add(Trajectories[i].Cut(0f, t));
                    secondHalf.Add(Trajectories[i].Cut(t, 1f));
                }
            }

            trajectory1 = firstHalf.Count == 1 ? firstHalf[0] : new CombinedTrajectory(firstHalf);
            trajectory2 = secondHalf.Count == 1 ? secondHalf[0] : new CombinedTrajectory(secondHalf);
        }

        public float3 Position(float t) {
            t = ToPartT(t, out var i);
            return Trajectories[i].Position(t);
        }

        public float3 Tangent(float t) {
            t = ToPartT(t, out var i);
            return Trajectories[i].Tangent(t);
        }

        public float Travel(float distance) => Travel(0f, distance);

        public float Travel(float start, float distance) {
            for (var i = 0; i < Count; i += 1) {
                var length = Trajectories[i].Length;
                if (length <= distance) {
                    distance -= length;
                } else {
                    return FromPartT(i, Trajectories[i].Travel(distance));
                }
            }

            return 1f;
        }

        public float Distance(float from = 0f, float to = 1f) {
            from = ToPartT(from, out var startI);
            to = ToPartT(to, out var endI);

            if (startI == endI) {
                return Trajectories[startI].Distance(from, to);
            }

            var distance = Trajectories[startI].Distance(from, 1f);
            for (var i = startI + 1; i < endI; i += 1) {
                distance += Trajectories[i].Length;
            }

            distance += Trajectories[endI].Distance(0f, to);
            return distance;
        }

        public CombinedTrajectory Invert() =>
            new CombinedTrajectory(Trajectories.Select(t => t.Invert()).Reverse().ToArray(), m_Length,
                m_Parts?.Reverse().ToArray(), Magnitude, DeltaAngle, -Direction, EndDirection, StartDirection);
        ITrajectory ITrajectory.Invert() => Invert();

        public CombinedTrajectory Shift(float start, float end) {
            var trajectories = new List<ITrajectory>();
            var parts = Parts;
            for (var i = 0; i < parts.Length; i += 1) {
                var startI = math.lerp(start, end, parts[i]);
                var endI = math.lerp(start, end, i + 1 < parts.Length ? parts[i + 1] : 1f);
                trajectories.Add(Trajectories[i].Shift(startI, endI));
            }

            return new CombinedTrajectory(trajectories);
        }
        ITrajectory ITrajectory.Shift(float start, float end) => Shift(start, end);

        public CombinedTrajectory Elevate(float height) => new CombinedTrajectory(Trajectories.Select(t => t.Elevate(height)));

        public CombinedTrajectory Elevate(float start, float end) {
            var trajectories = new List<ITrajectory>();
            var parts = Parts;
            for (var i = 0; i < parts.Length; i += 1) {
                var startI = math.lerp(start, end, parts[i]);
                var endI = math.lerp(start, end, i + 1 < parts.Length ? parts[i + 1] : 1f);
                trajectories.Add(Trajectories[i].Elevate(startI, endI));
            }

            return new CombinedTrajectory(trajectories);
        }
        ITrajectory ITrajectory.Elevate(float height) => Elevate(height);
        ITrajectory ITrajectory.Elevate(float start, float end) => Elevate(start, end);

        public float3 GetClosestPosition(float3 point, out float t) {
            GetClosestPositionAndDirection(point, out var position, out _, out t);
            return position;
        }

        public void GetClosestPositionAndDirection(float3 point, out float3 position, out float3 direction, out float t) {
            var bestDistance = float.MaxValue;
            position = StartPosition;
            direction = StartDirection;
            t = 0f;

            for (var i = 0; i < Count; i += 1) {
                Trajectories[i].GetClosestPositionAndDirection(point, out var candidatePosition, out var candidateDirection, out var candidateT);
                var distance = math.distance(point, candidatePosition);
                if (distance < bestDistance) {
                    bestDistance = distance;
                    position = candidatePosition;
                    direction = candidateDirection;
                    t = FromPartT(i, candidateT);
                }
            }
        }

        public override string ToString() => string.Join("\n", Trajectories.Select(t => t.ToString()).ToArray());
    }
}
