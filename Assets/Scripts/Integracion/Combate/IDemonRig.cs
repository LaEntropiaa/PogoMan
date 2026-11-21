// Cualquier animador de demonio (zombie, hongo...) que se pueda poner en una pose.
// ZombieMotor lo usa para hornear la malla fija que se ve de lejos.
public interface IDemonRig
{
    void ApplyPose(float phase, float moveBlend, float attackProgress, float time);
}
