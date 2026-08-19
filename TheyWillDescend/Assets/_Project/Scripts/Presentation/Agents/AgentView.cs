using UnityEngine;

namespace TheyWillDescend.Presentation.Agents
{
    /// <summary>
    /// Skinned view only. Does not create entities or own simulation state.
    /// </summary>
    public sealed class AgentView : MonoBehaviour
    {
        [SerializeField] string walkBoolParameter = "Walk 1";

        Animator _animator;

        public void Bind()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null || string.IsNullOrEmpty(walkBoolParameter))
                return;

            foreach (var p in _animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool)
                    _animator.SetBool(p.name, p.name == walkBoolParameter);
            }
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
