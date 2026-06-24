using UnityEngine;

namespace _Scripts.CombatSystem.RenderRange
{
    public class RenderRangeSpawner : MonoBehaviour
    {
        [SerializeField] private RenderSkillRange skillRangePrefab;
        [SerializeField] private RenderRangeProjectionMode projectionMode = RenderRangeProjectionMode.DecalProjector;
        [SerializeField, Range(-100, 100)] private int renderPriority;
        [SerializeField] private bool destroyAfterFill = true;
        [SerializeField, Min(0f)] private float destroyDelay = 0.15f;

        public RenderSkillRange SpawnCircle(Vector3 position, float radius, float duration,
            RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial)
        {
            RenderSkillRange skillRange = SpawnBase(position, Quaternion.identity, duration, playback);
            skillRange.ConfigureCircle(radius);
            skillRange.Play();
            ScheduleDestroy(skillRange, duration);
            return skillRange;
        }

        public RenderSkillRange SpawnDonut(Vector3 position, float radius, float innerRadius, float duration,
            RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial)
        {
            RenderSkillRange skillRange = SpawnBase(position, Quaternion.identity, duration, playback);
            skillRange.ConfigureDonut(radius, innerRadius);
            skillRange.Play();
            ScheduleDestroy(skillRange, duration);
            return skillRange;
        }

        public RenderSkillRange SpawnCone(Vector3 position, Quaternion rotation, float radius, float angle,
            float duration, RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial)
        {
            RenderSkillRange skillRange = SpawnBase(position, rotation, duration, playback);
            skillRange.ConfigureCone(radius, angle);
            skillRange.Play();
            ScheduleDestroy(skillRange, duration);
            return skillRange;
        }

        public RenderSkillRange SpawnRectangle(Vector3 position, Quaternion rotation, float length, float width,
            float duration, RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial)
        {
            RenderSkillRange skillRange = SpawnBase(position, rotation, duration, playback);
            skillRange.ConfigureRectangle(length, width);
            skillRange.Play();
            ScheduleDestroy(skillRange, duration);
            return skillRange;
        }

        public RenderSkillRange SpawnLine(Vector3 position, Quaternion rotation, float length, float width,
            float duration, RenderRangePlayback playback = RenderRangePlayback.FillAfterInitial)
        {
            RenderSkillRange skillRange = SpawnBase(position, rotation, duration, playback);
            skillRange.ConfigureLine(length, width);
            skillRange.Play();
            ScheduleDestroy(skillRange, duration);
            return skillRange;
        }

        private RenderSkillRange SpawnBase(Vector3 position, Quaternion rotation, float duration,
            RenderRangePlayback playback)
        {
            RenderSkillRange skillRange;
            if (skillRangePrefab != null)
            {
                skillRange = Instantiate(skillRangePrefab, position, rotation);
            }
            else
            {
                GameObject rangeObject = new GameObject("LostArk Render Range");
                rangeObject.transform.SetPositionAndRotation(position, rotation);
                skillRange = rangeObject.AddComponent<RenderSkillRange>();
            }

            skillRange.SetProjectionMode(projectionMode);
            skillRange.SetRenderPriority(renderPriority);
            skillRange.SetDuration(duration);
            skillRange.SetPlayback(playback);
            return skillRange;
        }

        private void ScheduleDestroy(RenderSkillRange skillRange, float duration)
        {
            if (!destroyAfterFill || skillRange == null) return;

            Destroy(skillRange.gameObject, Mathf.Max(0.01f, duration + destroyDelay));
        }
    }
}
