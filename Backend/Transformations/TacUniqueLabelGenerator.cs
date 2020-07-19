using System.Collections.Generic;
using System.Linq;
using Model;
using Model.ThreeAddressCode.Instructions;

namespace Backend.Transformations
{
    public class TacUniqueLabelGenerator
    {
        private readonly IList<Instruction> instructions;
        private uint offset;

        public TacUniqueLabelGenerator(IList<Instruction> instructions)
        {
            this.instructions = instructions;
        }

        // FIXME Si esto anda, documentar donde se usa, esto de que para agregar isntrucciones hay que agregarlas con el mismo label
        // de la instruccion anterior a donde agrego, etc
        // ver ademas si tiene sentido hacerlo aca, o mientras se genera el tac. Tener en cuenta que las instrumentaciones se harian
        // post generacion del tac.
        // FIXME se puede optimizar? son varias pasadas sino por las instrucciones
        public void Execute()
        {
            var originalTargets = new HashSet<string>();
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

            var newBranchTargets = new Dictionary<string, string>();

            foreach (var instruction in instructions)
            {
                // fixme lo de chequear que no este es para no pisarla si es una repetida
                if (originalTargets.Contains(instruction.Label) && !newBranchTargets.TryGetValue(instruction.Label, out _))
                {
                    newBranchTargets[instruction.Label] = string.Format("L_{0:X4}", offset);
                }

                instruction.Offset = offset;
                instruction.Label = string.Format("L_{0:X4}", offset);
                offset++;
            }

            foreach (var instruction in instructions)
            {
                // FIXME esto es porque no necesariamente necesita traduccion. Ejemplo, hay algunas branch que saltan al siguiente entonces
                // hay mas casos? Esta bien esto?
                switch (instruction)
                {
                    case BranchInstruction branchInstruction:
                    {
                        if (newBranchTargets.TryGetValue(branchInstruction.Target, out var newTarget))
                        {
                            branchInstruction.Target = newTarget;
                        }

                        break;
                    }
                    case SwitchInstruction switchInstruction:
                        switchInstruction.Targets = switchInstruction.Targets
                            .Select(target => newBranchTargets.TryGetValue(target, out var newTarget)
                                ? newTarget
                                : target)
                            .ToList();
                        break;
                }
            }
        }
    }
}