namespace NuGetResolver.Editor {
  internal readonly struct ProgressSegment {
    private readonly float _min;
    private readonly float _max;

    public ProgressSegment(float min, float max) {
      _min = min;
      _max = max;
    }

    public float Evaluate(float progress) {
      return _min + (_max - _min) * progress;
    }
  }
}
