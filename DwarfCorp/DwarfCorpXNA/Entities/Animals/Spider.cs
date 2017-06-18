using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DwarfCorp;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class Spider : Creature
    {

        public Spider()
        {

        }

        public Spider(ComponentManager manager, string sprites, Vector3 position) :
            base
            (
                manager,
                new CreatureStats
                {
                    Dexterity = 6,
                    Constitution = 3,
                    Strength = 3,
                    Wisdom = 3,
                    Charisma = 3,
                    Intelligence = 3,
                    Size = 0.25f,
                    CanSleep = false
                },
                "Carnivore",
                manager.World.PlanService,
                manager.World.Factions.Factions["Carnivore"],
                "Spider"
            )
        {
            Physics = new Physics
                (
                    manager, 
                    // It is called "bird"
                    "Spider",
                   // It's attached to the root component of the component manager
                    // It is located at a position passed in as an argument
                    Matrix.CreateTranslation(position),
                    // It has a size of 0.25 blocks
                    new Vector3(0.375f, 0.375f, 0.375f),
                    // Its bounding box is located in its center
                    new Vector3(0.0f, 0.0f, 0.0f),
                    //It has a mass of 1, a moment of intertia of 1, and very small friction/restitution
                    1.0f, 1.0f, 0.999f, 0.999f,
                    // It has a gravity of 10 blocks per second downward
                    new Vector3(0, -10, 0)
                );

            Physics.AddChild(this);

            SelectionCircle = Physics.AddChild(new SelectionCircle(Manager)
            {
                IsVisible = false
            }) as SelectionCircle;

            Initialize(sprites);
        }

        /// <summary>
        /// Initialize function creates all the required components for the bird.
        /// </summary>
        /// <param name="sprites">The sprite sheet to use for the bird</param>
        public void Initialize(string sprites)
        {
            HasBones = false;
            // When true, causes the bird to face the direction its moving in
            Physics.Orientation = Physics.OrientMode.RotateY;


            // Create the sprite component for the bird.
            Sprite = Physics.AddChild(new CharacterSprite
                                  (Graphics,
                                  Manager,
                                  "Spider Sprite",
                                  Matrix.CreateTranslation(0, 0.5f, 0)
                                  )) as CharacterSprite;

            CompositeAnimation.Descriptor descriptor =
                FileUtils.LoadJsonFromString<CompositeAnimation.Descriptor>(
                    ContentPaths.GetFileAsString(sprites));

            List<CompositeAnimation> animations = descriptor.GenerateAnimations("Spider");

            foreach (CompositeAnimation animation in animations)
            {
                Sprite.AddAnimation(animation);
            }

            // Used to grab other components
            Hands = Physics.AddChild(new Grabber("hands", Manager, Matrix.Identity, new Vector3(0.2f, 0.2f, 0.2f), Vector3.Zero)) as Grabber;

            // Used to sense hostile creatures
            Sensors = Physics.AddChild(new EnemySensor(Manager, "EnemySensor", Matrix.Identity, new Vector3(20, 5, 20), Vector3.Zero)) as EnemySensor;

            // Controls the behavior of the creature
            AI = Physics.AddChild(new PacingCreatureAI(Manager, "Spider AI", Sensors, PlanService)) as CreatureAI;

            // The bird can peck at its enemies (0.1 damage)
            Attacks = new List<Attack> { new Attack("Sting", 0.01f, 1.0f, 3.0f, SoundSource.Create(ContentPaths.Audio.hiss), ContentPaths.Effects.bite),
                new Attack("Web", 0.0f, 1.0f, 5.0f, SoundSource.Create(ContentPaths.Audio.hiss), ContentPaths.Effects.claws) {Mode = Attack.AttackMode.Ranged, LaunchSpeed = 10, ProjectileType = "Web"} };


            // The bird can hold one item at a time in its inventory
            Inventory = Physics.AddChild(new Inventory(Manager, "Inventory", Physics.BoundingBox.Extents(), Physics.BoundingBoxPos)
            {
                Resources = new ResourceContainer
                {
                    MaxResources = 1
                }
            }) as Inventory;

            // The shadow is rotated 90 degrees along X, and is 0.25 blocks beneath the creature
            Matrix shadowTransform = Matrix.CreateRotationX((float)Math.PI * 0.5f);
            shadowTransform.Translation = new Vector3(0.0f, -0.25f, 0.0f);
            shadowTransform *= Matrix.CreateScale(0.75f);

            SpriteSheet shadowTexture = new SpriteSheet(ContentPaths.Effects.shadowcircle);
            var shadow = Physics.AddChild(new Shadow(Manager, "Shadow", shadowTransform, shadowTexture)) as Shadow;

            // We set up the shadow's animation so that it's just a static black circle
            // TODO: Make the shadow set this up automatically
            List<Point> shP = new List<Point>
            {
                new Point(0, 0)
            };
            Animation shadowAnimation = new Animation(Graphics, new SpriteSheet(ContentPaths.Effects.shadowcircle), "sh", 32, 32, shP, false, Color.Black, 1, 0.7f, 0.7f, false);
            shadow.AddAnimation(shadowAnimation);
            shadowAnimation.Play();
            shadow.SetCurrentAnimation("sh");

            // The bird will emit a shower of blood when it dies
            Physics.AddChild(new ParticleTrigger("blood_particle", Manager, "Death Gibs", Matrix.Identity, Vector3.One, Vector3.Zero)
            {
                TriggerOnDeath = true,
                TriggerAmount = 1
            });

            // The bird is flammable, and can die when exposed to fire.
            Physics.AddChild(new Flammable(Manager, "Flames"));

            // Tag the physics component with some information 
            // that can be used later
            Physics.Tags.Add("Spider");
            Physics.Tags.Add("Animal");

            Stats.FullName = TextGenerator.GenerateRandom("$firstname") + " the Spider";
            Stats.CurrentClass = new EmployeeClass()
            {
                Name = "Spider",
                Levels = new List<EmployeeClass.Level>() { new EmployeeClass.Level() { Index = 0, Name = "Spider" } }
            };

            NoiseMaker.Noises["Hurt"] = new List<string>() { ContentPaths.Audio.hiss };
            AI.Movement.CanClimbWalls = true;
            AI.Movement.CanSwim = false;
            Species = "Spider";
            CanReproduce = true;
            BabyType = "Spider";
        }

    }

    public class PacingCreatureAI : CreatureAI
    {
        public PacingCreatureAI()
        {

        }

        public PacingCreatureAI(ComponentManager Manager, string name, EnemySensor sensors, PlanService planService) :
            base(Manager, name, sensors, planService)
        {

        }

        public override Act ActOnWander()
        {
            return new WanderAct(this, 6, 0.5f + MathFunctions.Rand(-0.25f, 0.25f), 1.0f) & new LongWanderAct(this) { PathLength = 10, Radius = 50 };
        }
    }
   
}
