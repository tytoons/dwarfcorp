﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp
{
    class LookInteresting : Goal
    {

        public LookInteresting(GOAP agent)
        {
            Name = "LookInteresting";
            Priority = 0.001f;
            Reset(agent);
        }

        public override List<Action> GetPresetPlan(CreatureAIComponent creature, GOAP agent)
        {
            List<Action> wander = new List<Action>();
            wander.Add(new Wander());
            return wander;
        }

        public override void ContextReweight(CreatureAIComponent creature)
        {
            Priority = 0.001f;
            Cost = 0.001f;
            base.ContextReweight(creature);
        }

        public override bool ContextValidate(CreatureAIComponent creature)
        {
            return (GOAP.MotionStatus)creature.Goap.Belief[GOAPStrings.MotionStatus] == GOAP.MotionStatus.Stationary;
        }

        public override void Reset(GOAP agent)
        {
            State[GOAPStrings.MotionStatus] = GOAP.MotionStatus.Moving;

            base.Reset(agent);
        }



    }
}
