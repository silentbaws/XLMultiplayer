using System;
using RootMotion.FinalIK;
using UnityEngine;

public class MultiplayerIKController : MonoBehaviour {
	public void Start() {
	}
	
	public void FixedUpdate() {
		this.MoveAndRotateIKGameObject();
		this.LerpToTarget();
		this.SetSteezeWeight();
		this.SetIK();
	}
	
	public void OnAnimatorIK(int layer) {
		this.MoveAndRotateSkaterIKTargets();
	}
	
	public void LerpToTarget() {
		this._ikLeftPosLerp = Mathf.MoveTowards(this._ikLeftPosLerp, this._ikLeftLerpPosTarget, Time.deltaTime * this._lerpSpeed);
		this._ikRightPosLerp = Mathf.MoveTowards(this._ikRightPosLerp, this._ikRightLerpPosTarget, Time.deltaTime * this._lerpSpeed);
		this._ikLeftRotLerp = Mathf.MoveTowards(this._ikLeftRotLerp, this._ikLeftLerpRotTarget, Time.deltaTime * this._lerpSpeed);
		this._ikRightRotLerp = Mathf.MoveTowards(this._ikRightRotLerp, this._ikRightLerpRotTarget, Time.deltaTime * this._lerpSpeed);
	}
	
	public void SetSteezeWeight() {
		float target = Mathf.Clamp(this._leftSteezeTarget, 0f, this._leftSteezeMax);
		float target2 = Mathf.Clamp(this._rightSteezeTarget, 0f, this._rightSteezeMax);
		if (PlayerController.Instance.playerSM.LeftFootOffSM()) {
			target = 1f;
		}
		if (PlayerController.Instance.playerSM.RightFootOffSM()) {
			target2 = 1f;
		}
		this._leftSteezeWeight = Mathf.MoveTowards(this._leftSteezeWeight, target, Time.deltaTime * this._steezeLerpSpeed);
		this._rightSteezeWeight = Mathf.MoveTowards(this._rightSteezeWeight, target2, Time.deltaTime * this._steezeLerpSpeed);
		this._skaterLeftFootPos = Vector3.Lerp(this.skaterLeftFootTarget.position, this.steezeLeftFootTarget.position, this._leftSteezeWeight);
		this._skaterLeftFootRot = Quaternion.Slerp(this.skaterLeftFootTarget.rotation, this.steezeLeftFootTarget.rotation, this._leftSteezeWeight);
		this._skaterRightFootPos = Vector3.Lerp(this.skaterRightFootTarget.position, this.steezeRightFootTarget.position, this._rightSteezeWeight);
		this._skaterRightFootRot = Quaternion.Slerp(this.skaterRightFootTarget.rotation, this.steezeRightFootTarget.rotation, this._rightSteezeWeight);
	}
	
	public void SetIK() {
		this.ikLeftFootPosition.position = this.ikAnimLeftFootTarget.position;
		this.ikRightFootPosition.position = this.ikAnimRightFootTarget.position;
		this._finalIk.solver.leftFootEffector.position = Vector3.Lerp(this.ikLeftFootPositionOffset.position, this._skaterLeftFootPos, this._ikLeftPosLerp);
		this._finalIk.solver.rightFootEffector.position = Vector3.Lerp(this.ikRightFootPositionOffset.position, this._skaterRightFootPos, this._ikRightPosLerp);
		this._finalIk.solver.leftFootEffector.rotation = Quaternion.Slerp(this.ikAnimLeftFootTarget.rotation, this._skaterLeftFootRot, this._ikLeftRotLerp);
		this._finalIk.solver.rightFootEffector.rotation = Quaternion.Slerp(this.ikAnimRightFootTarget.rotation, this._skaterRightFootRot, this._ikRightRotLerp);
		this._finalIk.solver.leftFootEffector.positionWeight = Mathf.MoveTowards(this._finalIk.solver.leftFootEffector.positionWeight, this._leftPositionWeight, Time.deltaTime * 5f);
		this._finalIk.solver.rightFootEffector.positionWeight = Mathf.MoveTowards(this._finalIk.solver.rightFootEffector.positionWeight, this._rightPositionWeight, Time.deltaTime * 5f);
		this._finalIk.solver.rightFootEffector.rotationWeight = Mathf.MoveTowards(this._finalIk.solver.rightFootEffector.rotationWeight, this._rightRotationWeight, Time.deltaTime * 5f);
		this._finalIk.solver.leftFootEffector.rotationWeight = Mathf.MoveTowards(this._finalIk.solver.leftFootEffector.rotationWeight, this._leftRotationWeight, Time.deltaTime * 5f);
	}
	
	public void SetLeftIKOffset(float p_toeAxis, float p_forwardDir, float p_popDir, bool p_isPopStick, bool p_lockHorizontal, bool p_popping) {
		Vector3 localPosition = this.ikLeftFootPositionOffset.localPosition;
		if (!p_lockHorizontal) {
			localPosition.x = p_toeAxis * (p_popping ? this.popOffsetScaler : this.offsetScaler);
			if (!p_isPopStick) {
				localPosition.z = p_forwardDir * this.offsetScaler;
			}
			localPosition.y = 0f;
		} else {
			localPosition.y = 0f;
			localPosition.x = 0f;
			localPosition.z = p_forwardDir * this.offsetScaler;
		}
		if (SettingsManager.Instance.stance == SettingsManager.Stance.Goofy) {
			localPosition.x = -localPosition.x;
		}
		localPosition.y = -0.01f;
		this.ikLeftFootPositionOffset.localPosition = Vector3.Lerp(this.ikLeftFootPositionOffset.localPosition, localPosition, Time.deltaTime * 10f);
	}
	
	public void SetRightIKOffset(float p_toeAxis, float p_forwardDir, float p_popDir, bool p_isPopStick, bool p_lockHorizontal, bool p_popping) {
		Vector3 localPosition = this.ikRightFootPositionOffset.localPosition;
		if (!p_lockHorizontal) {
			localPosition.x = p_toeAxis * (p_popping ? this.popOffsetScaler : this.offsetScaler);
			if (!p_isPopStick) {
				localPosition.z = p_forwardDir * this.offsetScaler;
			}
			localPosition.y = 0f;
		} else {
			localPosition.y = 0f;
			localPosition.x = 0f;
			localPosition.z = p_forwardDir * this.offsetScaler;
		}
		if (SettingsManager.Instance.stance == SettingsManager.Stance.Goofy) {
			localPosition.x = -localPosition.x;
		}
		localPosition.y = 0.005f;
		this.ikRightFootPositionOffset.localPosition = Vector3.Lerp(this.ikRightFootPositionOffset.localPosition, localPosition, Time.deltaTime * 10f);
	}
	
	public void ResetIKOffsets() {
		Vector3 localPosition = this.ikRightFootPositionOffset.localPosition;
		localPosition.x = 0f;
		localPosition.z = 0f;
		this.ikRightFootPositionOffset.localPosition = localPosition;
		Vector3 localPosition2 = this.ikLeftFootPositionOffset.localPosition;
		localPosition2.x = 0f;
		localPosition2.z = 0f;
		this.ikLeftFootPositionOffset.localPosition = localPosition2;
	}
	
	public void MoveAndRotateIKGameObject() {
		if (PlayerController.Instance.playerSM.IsInImpactStateSM()) {
			if (!this._impactSet) {
				PlayerController.Instance.respawn.behaviourPuppet.BoostImmunity(1000f);
				this._impactSet = true;
			}
		} else if (this._impactSet) {
			this._impactSet = false;
		}
		this._ikAnim.velocity = this.physicsBoard.velocity;
		this._ikAnim.position = this.physicsBoard.position;
		Quaternion rhs = PlayerController.Instance.boardController.boardMesh.rotation;
		if (PlayerController.Instance.GetBoardBackwards()) {
			rhs = Quaternion.AngleAxis(180f, PlayerController.Instance.boardController.boardMesh.up) * rhs;
		}
		Vector3 vector = (!PlayerController.Instance.GetBoardBackwards()) ? PlayerController.Instance.boardController.boardMesh.forward : (-PlayerController.Instance.boardController.boardMesh.forward);
		Vector3 normalized = Vector3.ProjectOnPlane(PlayerController.Instance.skaterController.skaterTransform.up, vector).normalized;
		if (!PlayerController.Instance.IsGrounded() && !PlayerController.Instance.boardController.triggerManager.IsColliding) {
			rhs = Quaternion.LookRotation(vector, normalized);
		}
		Quaternion rhs2 = Quaternion.Inverse(this.ikAnimBoard.rotation) * rhs;
		this._ikAnim.rotation *= rhs2;
		this._ikAnim.angularVelocity = this.physicsBoard.angularVelocity;
		this._boardLastPos = this.physicsBoard.position;
	}
	
	public void MoveAndRotateSkaterIKTargets() {
		this.skaterLeftFootTargetParent.position = this.skaterLeftFoot.position;
		this.skaterLeftFootTargetParent.rotation = this.skaterLeftFoot.rotation;
		this.skaterRightFootTargetParent.position = this.skaterRightFoot.position;
		this.skaterRightFootTargetParent.rotation = this.skaterRightFoot.rotation;
	}
	
	public void SetIKRigidbodyKinematic(bool p_value) {
		this._ikAnim.isKinematic = p_value;
	}

	public void SetLeftLerpTarget(float pos, float rot) {
		this._ikLeftLerpPosTarget = pos;
		this._ikLeftLerpRotTarget = rot;
	}
	
	public void SetRightLerpTarget(float pos, float rot) {
		this._ikRightLerpPosTarget = pos;
		this._ikRightLerpRotTarget = rot;
	}
	
	public void SetMaxSteeze(float p_value) {
		this._leftSteezeMax = p_value;
		this._rightSteezeMax = p_value;
	}
	
	public void SetMaxSteezeLeft(float p_value) {
		this._leftSteezeMax = p_value;
	}
	
	public void SetMaxSteezeRight(float p_value) {
		this._rightSteezeMax = p_value;
	}
	
	public void SetLeftSteezeWeight(float p_value) {
		this._leftSteezeTarget = p_value;
	}
	
	public void SetRightSteezeWeight(float p_value) {
		this._rightSteezeTarget = p_value;
	}
	
	public void ForceLeftLerpValue(float p_value) {
		this._ikLeftPosLerp = p_value;
		this._ikLeftLerpPosTarget = p_value;
	}
	
	public void ForceRightLerpValue(float p_value) {
		this._ikRightPosLerp = p_value;
		this._ikRightLerpPosTarget = p_value;
	}
	
	public void OnOffIK(float p_value) {
		this._leftPositionWeight = p_value;
		this._rightPositionWeight = p_value;
		this._rightRotationWeight = p_value;
		this._leftRotationWeight = p_value;
		this._finalIk.solver.leftFootEffector.positionWeight = p_value;
		this._finalIk.solver.rightFootEffector.positionWeight = p_value;
		this._finalIk.solver.rightFootEffector.rotationWeight = p_value;
		this._finalIk.solver.leftFootEffector.rotationWeight = p_value;
	}
	
	public void LeftIKWeight(float p_value) {
		this._leftPositionWeight = p_value;
		this._leftRotationWeight = p_value;
		this._finalIk.solver.leftFootEffector.positionWeight = p_value;
		this._finalIk.solver.leftFootEffector.rotationWeight = p_value;
	}
	
	public void RightIKWeight(float p_value) {
		this._rightPositionWeight = p_value;
		this._rightRotationWeight = p_value;
		this._finalIk.solver.rightFootEffector.positionWeight = p_value;
		this._finalIk.solver.rightFootEffector.rotationWeight = p_value;
	}
	
	public void SetLeftIKRotationWeight(float p_value) {
	}
	
	public void SetRightIKRotationWeight(float p_value) {
	}
	
	public void SetKneeBendWeight(float p_value) {
		this._finalIk.solver.leftLegChain.bendConstraint.weight = p_value;
		this._finalIk.solver.rightLegChain.bendConstraint.weight = p_value;
	}
	
	public float GetKneeBendWeight() {
		return this._finalIk.solver.leftLegChain.bendConstraint.weight;
	}
	
	[SerializeField]
	public FullBodyBipedIK _finalIk;
	public Animator _anim;
	public AnimationCurve _animCurve;
	

	public Transform skaterLeftFoot;
	public Transform skaterRightFoot;
	
	public Transform skaterLeftFootTargetParent;
	public Transform skaterRightFootTargetParent;
	
	public Transform skaterLeftFootTarget;
	public Transform skaterRightFootTarget;
	
	public Transform steezeLeftFootTarget;
	public Transform steezeRightFootTarget;
	
	public Transform ikAnimLeftFootTarget;
	public Transform ikAnimRightFootTarget;
	
	public Transform ikLeftFootPosition;
	public Transform ikRightFootPosition;
	
	public Transform ikLeftFootPositionOffset;
	public Transform ikRightFootPositionOffset;

	public Transform ikAnimBoard;

	public Rigidbody physicsBoard;
	public Transform physicsBoardBackwards;
	
	public Rigidbody _ikAnim;
	
	public float _lerpSpeed = 10f;
	public float _steezeLerpSpeed = 2.5f;
	
	[Range(0f, 1f)]
	public float _ikLeftPosLerp;
	
	[Range(0f, 1f)]
	public float _ikLeftRotLerp;
	
	[Range(0f, 1f)]
	public float _ikLeftLerpPosTarget;
	
	[Range(0f, 1f)]
	public float _ikLeftLerpRotTarget;
	
	[Range(0f, 1f)]
	public float _ikRightPosLerp;
	
	[Range(0f, 1f)]
	public float _ikRightRotLerp;
	
	[Range(0f, 1f)]
	public float _ikRightLerpPosTarget;
	
	[Range(0f, 1f)]
	public float _ikRightLerpRotTarget;
	
	public float _rightPositionWeight = 1f;
	public float _leftPositionWeight = 1f;
	
	public float _rightRotationWeight = 1f;
	public float _leftRotationWeight = 1f;
	
	public float _leftSteezeMax;
	public float _rightSteezeMax;
	
	public float _leftSteezeTarget;
	public float _leftSteezeWeight;
	public float _rightSteezeTarget;
	public float _rightSteezeWeight;
	
	public Vector3 _skaterLeftFootPos = Vector3.zero;
	
	public Quaternion _skaterLeftFootRot = Quaternion.identity;
	
	public Vector3 _skaterRightFootPos = Vector3.zero;
	
	public Quaternion _skaterRightFootRot = Quaternion.identity;
	
	public float offsetScaler = 0.05f;
	
	public float popOffsetScaler = 0.5f;
	
	public Vector3 _boardPrevPos = Vector3.zero;

	public Vector3 _boardLastPos = Vector3.zero;
	
	public bool _impactSet;
}
