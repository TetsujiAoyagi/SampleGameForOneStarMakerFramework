#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// フェードの1歩。トークン源は作らない。時間は呼び出し側が進める。
    /// </summary>
    internal static class SoundFade
    {
        public static float Speed(float current, float target, float seconds)
        {
            if (seconds <= 0f)
            {
                return 0f;
            }

            var distance = target - current;
            if (distance < 0f)
            {
                distance = -distance;
            }

            if (distance == 0f)
            {
                return 0f;
            }

            return distance / seconds;
        }

        public static float Advance(float current, float target, float speed, float delta, out bool arrived)
        {
            if (speed <= 0f || delta <= 0f)
            {
                arrived = speed <= 0f;
                return speed <= 0f ? target : current;
            }

            var step = speed * delta;
            var next = current < target ? current + step : current - step;
            var reached = current < target ? next >= target : next <= target;
            if (reached)
            {
                arrived = true;
                return target;
            }

            arrived = false;
            return next;
        }
    }
}
