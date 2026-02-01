using UnityEngine;

public class CharacterPhysicsDriver : IPhysicsQuery
{
    private CharacterController _cc;
    private Transform _transform;

    public CharacterPhysicsDriver(CharacterController cc)
    {
        _cc = cc;
        _transform = cc.transform;
    }

    public void Move(Vector3 motion)
    {
        if (_cc.enabled)
        {
            _cc.Move(motion);
        }
    }

    //isGrounded只有在调用Move后才准确
    public bool IsGrounded => _cc.isGrounded;

    public Vector3 Position => _transform.position;

    public void SyncTransform(Vector3 position, Quaternion rotation)
    {
        _cc.enabled = false;
        _transform.position = position;
        _transform.rotation = rotation;
        _cc.enabled = true;
    }
    public void SetRotation(Quaternion rotation)
    {
        _transform.rotation = rotation;
    }
}