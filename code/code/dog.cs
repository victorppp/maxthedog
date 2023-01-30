using Sandbox.UI;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace pieper
{
    //credits to stubborn_birb addon (I used code from it for my addon)
    [Spawnable]
    [Library("ent_max_the_dog_npc", Title = "Max the dog")]
    public partial class Max_dog : ModelEntity
    {
        private AnimatedEntity targetPlayer;
        private Vector3 wanderPos = Vector3.Zero;
        private float wanderTime = 0f;
        private int currentWander = 0;
        private int wanderLimitRandom = 0;
        private float health = 100f;

        public float Speed { get; private set; }

        public override void Spawn()
        {
            base.Spawn();

            Predictable = true;

            var modelName = "model/dog.vmdl";

            SetModel(modelName);

            Bark();

            SetupPhysicsFromAABB(PhysicsMotionType.Static, Vector3.Zero, 15f); //Physics
            EnableSelfCollisions = true;
            PhysicsEnabled = true;
            UsePhysicsCollision = true;
            EnableSolidCollisions = true;

            var target = findTarget();
            if (!target.Item1)
            {
                Delete();
                Log.Error("There is no any player!");
                return;
            }

            targetPlayer = target.Item2;

        }

        private (bool, AnimatedEntity) findTarget()
        {
            var players = All.Where(x => x.Tags.Has("player"));

            if (!players.Any())
                return (false, null);

            var pickedply = players.ToList()[Game.Random.Int(players.Count() - 1)];

            return (true, (AnimatedEntity)pickedply);
        }

        private void Follow(bool wander = false, float rangeTarget = 0)
        {
            if (rangeTarget > (200 * 200))
            {
                Speed = Time.Delta / 4f;
            }
            else
            {
                wander = true;
            }

            if (wander)
            {
                if (wanderTime < Time.Now)
                {
                    wanderPos = (new Vector3(1, 1, 0) * (Position + Vector3.Random * 200f)) + (Vector3.Up * (Position + Vector3.Random * 10f)); //Z-axis should be less
                    wanderTime = Time.Now + 1;
                    currentWander++;
                }
            }
            else
                currentWander = 0;

            var substracted = ((wander ? wanderPos : targetPlayer.Position) - Position).EulerAngles;
            substracted.pitch /= 10;
            Rotation = substracted.ToRotation();
     
            
            Position = Position.LerpTo(wander ? wanderPos : (targetPlayer.Position + (Vector3.Up * targetPlayer.PhysicsBody.GetBounds().Size.x)), Speed);
        }

        private float RangeTarget() => Position.DistanceSquared(targetPlayer.Position + (Vector3.Up * targetPlayer.PhysicsBody.GetBounds().Size.z));

        public override void TakeDamage(DamageInfo info)
        {
            base.TakeDamage(info);

            health -= info.Damage;
            PlaySound("damage").SetVolume(5f).SetPitch(Game.Random.Float(1.25f, 1.55f));

            Particles.Create("particles/impact.flesh.vpcf", info.Position);

            if (health <= 0)
            {
                Sound.FromWorld(To.Everyone, "death", info.Position).SetVolume(10f);
                Particles.Create("particles/impact.flesh.bloodpuff.vpcf", info.Position);

                Delete();
            }
        }

        async public void Bark()
        {
            while (true) {
                PlaySound("bark").SetVolume(5f);
                await Task.Delay(Game.Random.Int(10000, 25000));
            }
        }

        [Event.Tick.Server]
        protected void Tick()
        {
             Follow(rangeTarget: RangeTarget());
        }
    }
}

