using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace pieper;

[Spawnable]
[Library("ent_max_the_dog_npc", Title = "Max the dog")]
public partial class Max_dog : ModelEntity
{
    private const float DEFAULT_HEALTH = 100f;
    private const string ALIVE_MODEL_NAME = "model/dog.vmdl";
    private const string DEAD_MODEL_NAME = "model/deaddog.vmdl";
    private const int PHYSICS_SIZE = 15;
    private const string DAMAGE_SOUND = "damage";
    private const string DEATH_SOUND = "death";
    private const string BARK_SOUND = "bark";
    private const string IMPACT_PARTICLE = "particles/impact.flesh.vpcf";
    private const string BLOOD_PARTICLE = "particles/impact.flesh.bloodpuff.vpcf";
    private const float DAMAGE_SOUND_VOLUME = 5f;
    private const float DEATH_SOUND_VOLUME = 10f;
    private const int DEATH_DELAY = 10000;

    private AnimatedEntity targetPlayer;
    private Vector3 wanderPos = Vector3.Zero;
    private float wanderTime = 0f;
    private int currentWander = 0;
    private float health = DEFAULT_HEALTH;
    private bool alive = true;
    private static Random rnd = new Random();


    public float Speed { get; private set; }

    public override void Spawn()
    {
        base.Spawn();
        Predictable = true;

        SetModel(ALIVE_MODEL_NAME);
        SetCollisions(true);
        _ = Bark();

        var (found, player) = FindTarget();
        if (!found)
        {
            Delete();
            Log.Error("No player found!");
            return;
        }

        targetPlayer = player;
    }

    private void SetCollisions(bool state)
    {
        SetupPhysicsFromAABB(PhysicsMotionType.Static, Vector3.Zero, PHYSICS_SIZE);
        EnableSelfCollisions = state;
        PhysicsEnabled = state;
        UsePhysicsCollision = state;
        EnableSolidCollisions = state;
    }

    private static (bool found, AnimatedEntity player) FindTarget()
    {
        var player = All.Where(x => x.Tags.Has("player") && x.Owner.ToString() == Game.SteamId.ToString() + "/" + Game.UserName.ToString());
        return player.Any() ? (true, (AnimatedEntity)player.First()) : (false, null);
    }

    private void Follow(bool wander = false, float rangeTarget = 0)
    {
        UpdatePositionBasedOnRange(rangeTarget);
        UpdateSpeedAndWanderingStatus(rangeTarget, ref wander);
        UpdateWanderingPositionIfNecessary(wander);

        UpdateEntityRotation(wander);
        LerpEntityPosition(wander);
    }

    private void UpdatePositionBasedOnRange(float rangeTarget)
    {
        const int FAR_RANGE_SQUARE = 900 * 900;

        if (rangeTarget > FAR_RANGE_SQUARE)
        {
            Position = targetPlayer.Position + 50f;
        }
    }

    private void UpdateSpeedAndWanderingStatus(float rangeTarget, ref bool wander)
    {
        const int CLOSE_RANGE_SQUARE = 200 * 200;

        if (rangeTarget > CLOSE_RANGE_SQUARE)
        {
            Speed = Time.Delta / 4f;
        }
        else
        {
            wander = true;
        }
    }

    private void UpdateWanderingPositionIfNecessary(bool wander)
    {
        if (wander && wanderTime < Time.Now)
        {
            wanderPos = (new Vector3(1, 1, 0) * (Position + Vector3.Random * 200f)) + (Vector3.Up * (Position + Vector3.Random * 10f)); // Z-axis should be less
            wanderTime = Time.Now + 1;
            currentWander++;
        }
        else if (!wander)
        {
            currentWander = 0;
        }
    }

    private void UpdateEntityRotation(bool wander)
    {
        var targetPosition = wander ? wanderPos : targetPlayer.Position;
        var rotationDelta = (targetPosition - Position).EulerAngles;

        rotationDelta.pitch /= 10;
        Rotation = rotationDelta.ToRotation();
    }

    private void LerpEntityPosition(bool wander)
    {
        var targetPosition = wander
            ? wanderPos
            : targetPlayer.Position + (Vector3.Up * targetPlayer.PhysicsBody.GetBounds().Size.x);
        Position = Position.LerpTo(targetPosition, Speed);
    }

    private float RangeTarget() => Position.DistanceSquared(targetPlayer.Position);

    public override void TakeDamage(DamageInfo info)
    {
        base.TakeDamage(info);

        if (alive)
        {
            ProcessAliveDamage(info.Damage);
        }

        CreateParticle(IMPACT_PARTICLE, info.Position);

        if (health <= 0 && alive)
        {
            HandleDeath(info.Position);
        }
    }

    private void ProcessAliveDamage(float damage)
    {
        health -= damage;
        PlaySound(DAMAGE_SOUND)
            .SetVolume(DAMAGE_SOUND_VOLUME)
            .SetPitch(Game.Random.Float(1.25f, 1.55f));
    }

    private void HandleDeath(Vector3 deathPosition)
    {
        Sound.FromWorld(To.Everyone, DEATH_SOUND, deathPosition)
            .SetVolume(DEATH_SOUND_VOLUME);

        _ = Death();
        SetModel(DEAD_MODEL_NAME);
        SetCollisions(true);
        alive = false;

        CreateParticle(BLOOD_PARTICLE, deathPosition);
    }

    private static void CreateParticle(string particle, Vector3 position)
    {
        Particles.Create(particle, position);
    }

    public async Task Death()
    {
        await Task.Delay(DEATH_DELAY);
        Delete();
    }

    public async Task Bark()
    {
        if (alive) {
            PlaySound(BARK_SOUND).SetVolume(5f);
            await Task.Delay(Game.Random.Int(10000, 25000));
        }
    }

    [GameEvent.Tick.Server]
    protected void Tick()
    {
        if (alive) 
        {
            Follow(rangeTarget: RangeTarget());
        }
    }
}
