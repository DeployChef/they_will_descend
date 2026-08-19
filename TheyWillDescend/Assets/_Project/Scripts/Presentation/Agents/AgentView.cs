using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Skinned view only. Pulls Moving from sim; does not own locomotion.
    /// </summary>
    public sealed class AgentView : MonoBehaviour
    {
        [SerializeField] string walkBoolParameter = "Walk 1";

        Animator _animator;

        public void Bind()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null)
                _animator.applyRootMotion = false;
            SetMoving(false);
        }

        public void SetOnField(bool onField)
        {
            if (gameObject.activeSelf != onField)
                gameObject.SetActive(onField);
        }

        public void SetMoving(bool moving)
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null || string.IsNullOrEmpty(walkBoolParameter))
                return;

            _animator.SetBool(walkBoolParameter, moving);
        }

        public void SetAnimSpeed(float speed)
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator != null)
                _animator.speed = speed;
        }
    }
}
