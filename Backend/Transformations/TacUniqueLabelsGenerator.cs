using System.Collections.Generic;
using System.Linq;
using Model;
using Model.ThreeAddressCode.Instructions;

namespace Backend.Transformations
{
    public class TacUniqueLabelsGenerator
    {
        private readonly IList<Instruction> instructions;
        private uint offset;

        public TacUniqueLabelsGenerator(IList<Instruction> instructions)
        {
            this.instructions = instructions;
        }

        // FIXME documentar donde se usa, esto de que para agregar isntrucciones hay que agregarlas con el mismo label
        // de la instruccion anterior a donde agrego, etc
        // FIXME se puede optimizar? son varias pasadas sino por las instrucciones
        public void Execute()
        {
            var originalTargets = new HashSet<string>();
            var newTargets = new Dictionary<string, string>();
            foreach (var instruction in instructions)
            {
                switch (instruction)
                {
                    case BranchInstruction branchInstruction:
                        originalTargets.Add(branchInstruction.Target);
                        break;
                    case SwitchInstruction switchInstruction:
                        originalTargets.AddRange(switchInstruction.Targets);
                        break;
                }
            }

            // generate unique labels for all instructions and gather new target labels
            foreach (var instruction in instructions)
            {
                var newLabel = $"L_{offset:X4}";
                if (originalTargets.Contains(instruction.Label) && !newTargets.TryGetValue(instruction.Label, out _))
                {
                    newTargets[instruction.Label] = newLabel;
                }

                instruction.Offset = offset;
                instruction.Label = newLabel;
                offset++;
            }

            // replace old targets
            foreach (var instruction in instructions)
            {
                switch (instruction)
                {
                    case BranchInstruction branchInstruction:
                    {
                        if (newTargets.TryGetValue(branchInstruction.Target, out var newTarget))
                        {
                            branchInstruction.Target = newTarget;
                        }

                        break;
                    }
                    case SwitchInstruction switchInstruction:
                        switchInstruction.Targets = switchInstruction.Targets
                            .Select(target => newTargets.TryGetValue(target, out var newTarget)
                                ? newTarget
                                : target)
                            .ToList();
                        break;
                }
            }
        }
    }
}