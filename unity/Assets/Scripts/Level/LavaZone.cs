using UnityEngine;

namespace TooFishy
{
    public class LavaZone : MonoBehaviour
    {
        void OnTriggerStay(Collider other)
        {
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;
            GameState.Instance.Damage(10f * Time.deltaTime);
            player.AddTrauma(0.05f);
        }
    }
}
