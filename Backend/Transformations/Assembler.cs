using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using ConvertInstruction = Model.ThreeAddressCode.Instructions.ConvertInstruction;
using CreateArrayInstruction = Model.ThreeAddressCode.Instructions.CreateArrayInstruction;
using CreateObjectInstruction = Model.ThreeAddressCode.Instructions.CreateObjectInstruction;
using IndirectMethodCallInstruction = Model.ThreeAddressCode.Instructions.IndirectMethodCallInstruction;
using LoadInstruction = Model.ThreeAddressCode.Instructions.LoadInstruction;
using LoadTokenInstruction = Model.ThreeAddressCode.Instructions.LoadTokenInstruction;
using MethodCallInstruction = Model.ThreeAddressCode.Instructions.MethodCallInstruction;
using SizeofInstruction = Model.ThreeAddressCode.Instructions.SizeofInstruction;
using StoreInstruction = Model.ThreeAddressCode.Instructions.StoreInstruction;
using SwitchInstruction = Model.ThreeAddressCode.Instructions.SwitchInstruction;
using Bytecode = Model.Bytecode;


namespace Backend.Transformations
{
    public class Assembler
    {
        private readonly MethodDefinition method;

        public Assembler(MethodDefinition method)
        {
            if (!method.Body.Kind.Equals(MethodBodyKind.ThreeAddressCode))
            {
                throw new Exception("MethodBody must be in Three Address Code");
            }

            this.method = method;
        }

        public MethodBody Execute()
        {
            var body = new MethodBody(MethodBodyKind.Bytecode);

            body.MaxStack = method.Body.MaxStack; // FIXME 
            body.Parameters.AddRange(method.Body.Parameters);
            body.LocalVariables.AddRange(method.Body.LocalVariables);

            if (method.Body.Instructions.Count > 0)
            {
                new InstructionTranslator(body).Visit(method.Body);
            }

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            private readonly MethodBody body;
            private readonly Stack<ProtectedBlockBuilder> protectedBlocks = new Stack<ProtectedBlockBuilder>();

            public InstructionTranslator(MethodBody body)
            {
                this.body = body;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation));
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(LoadInstruction instruction)
            {
                // translate to dup, loadArrayLength, loadArrayElement, loadArrayElementAddress, loadStaticField, loadInstanceField,
                // loadStaticFieldAddress, loadInnstanceFieldAddress, loadIndirect, loadConstant, loadVariable, loadVariableAddress, 
                // loadStaticMethodAddress, loadVirtualMethodAddress,
                // StoreInstruction? CreateObjectInstruction? (se genera una en el visit de ambos)
                // FIXME revisar los casos, hay algunos que no estoy seguro de que esten bien. se repiten caminos ademas (sobretodo por el reference)

                Bytecode.Instruction loadInstruction;
                if (instruction.Operand is TemporalVariable && instruction.Result is TemporalVariable)
                {
                    if (instruction.Operand.Equals(instruction.Result))
                    {
                        return;
                    }
                    else
                    {
                        loadInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Dup);
                    }
                }
                else
                {
                    switch (instruction.Operand)
                    {
                        case Constant constant:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Value, constant);
                            break;
                        case TemporalVariable _:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Content, instruction.Result);
                            break;
                        case Dereference dereference:
                            loadInstruction = new Bytecode.LoadIndirectInstruction(instruction.Offset, dereference.Type);
                            break;
                        case Reference reference:
                            switch (reference.Value)
                            {
                                case ArrayElementAccess _:
                                case LocalVariable _:
                                    loadInstruction = new Bytecode.LoadInstruction(
                                        instruction.Offset,
                                        Bytecode.LoadOperation.Address,
                                        instruction.Result);
                                    break;
                                case InstanceFieldAccess instanceFieldAccess:
                                    // fixme es content?
                                    loadInstruction = new Bytecode.LoadFieldInstruction(instruction.Offset, Bytecode.LoadFieldOperation.Content,
                                        instanceFieldAccess.Field);
                                    break;
                                default:
                                    throw new Exception(); // TODO
                            }

                            break;
                        case LocalVariable localVariable:
                            loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Content, localVariable);
                            break;
                        case ArrayLengthAccess _:
                            loadInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LoadArrayLength);
                            break;
                        case StaticMethodReference staticMethodReference:
                            loadInstruction = new Bytecode.LoadMethodAddressInstruction(
                                instruction.Offset,
                                Bytecode.LoadMethodAddressOperation.Static,
                                staticMethodReference.Method);
                            break;
                        case InstanceFieldAccess instanceFieldAccess:
                            // fixme es content?
                            loadInstruction = new Bytecode.LoadFieldInstruction(instruction.Offset, Bytecode.LoadFieldOperation.Content,
                                instanceFieldAccess.Field);
                            break;
                        case StaticFieldAccess staticFieldAccess:
                            // fixme es content?
                            loadInstruction = new Bytecode.LoadFieldInstruction(instruction.Offset, Bytecode.LoadFieldOperation.Content,
                                staticFieldAccess.Field);
                            break;
                        case ArrayElementAccess arrayElementAccess:
                            loadInstruction = new Bytecode.LoadArrayElementInstruction(
                                instruction.Offset,
                                Bytecode.LoadArrayElementOperation.Content,
                                (ArrayType) arrayElementAccess.Array.Type);
                            break;
                        default: throw new Exception(); // TODO
                    }
                }

                body.Instructions.Add(loadInstruction);
            }

            public override void Visit(StoreInstruction instruction)
            {
                Bytecode.Instruction storeInstruction;
                switch (instruction.Result)
                {
                    case ArrayElementAccess arrayElementAccess:
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(instruction.Offset, (ArrayType) arrayElementAccess.Array.Type);
                        break;
                    case Dereference dereference:
                        storeInstruction = new Bytecode.StoreIndirectInstruction(instruction.Offset, dereference.Type);
                        break;
                    case InstanceFieldAccess instanceFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, instanceFieldAccess.Field);
                        break;
                    case StaticFieldAccess staticFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, staticFieldAccess.Field);
                        break;
                    default:
                        throw new Exception(); // TODO msg
                }

                body.Instructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(TryInstruction instruction)
            {
                // try with multiple handlers are modelled as multiple try instructions with the same label but different handlers.
                // if label matches with the current try, then increase the number of expected handlers
                if (protectedBlocks.Count > 0 && protectedBlocks.Peek().TryStart.Equals(instruction.Offset))
                {
                    protectedBlocks.Peek().HandlerCount++;
                }
                else
                {
                    var exceptionBlockBuilder = new ProtectedBlockBuilder {TryStart = instruction.Offset, HandlerCount = 1};
                    protectedBlocks.Push(exceptionBlockBuilder);
                }
            }

            public override void Visit(FaultInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .Handlers
                    .Add(new ExceptionHandlerBlockBuilder
                    {
                        HandlerStart = instruction.Offset,
                        HandlerBlockKind = ExceptionHandlerBlockKind.Fault,
                    });
            }

            public override void Visit(FinallyInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .Handlers
                    .Add(
                        new ExceptionHandlerBlockBuilder
                        {
                            HandlerStart = instruction.Offset,
                            HandlerBlockKind = ExceptionHandlerBlockKind.Finally,
                        });
            }

            public override void Visit(FilterInstruction instruction)
            {
                var handlers = protectedBlocks.Peek().Handlers;
                switch (instruction.kind)
                {
                    case FilterInstructionKind.FilterSection:
                        handlers.Add(
                            new ExceptionHandlerBlockBuilder
                            {
                                FilterStart = instruction.Offset,
                                HandlerBlockKind = ExceptionHandlerBlockKind.Filter,
                            });
                        break;
                    case FilterInstructionKind.FilterHandler:
                        var handler = handlers.Last();
                        handler.HandlerStart = instruction.Offset;
                        handler.ExceptionType = instruction.ExceptionType;
                        break;
                    default: throw instruction.kind.ToUnknownValueException();
                }
            }

            public override void Visit(CatchInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .Handlers
                    .Add(
                        new ExceptionHandlerBlockBuilder()
                        {
                            HandlerStart = instruction.Offset,
                            HandlerBlockKind = ExceptionHandlerBlockKind.Catch,
                            ExceptionType = instruction.ExceptionType
                        }
                    );
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var convertInstruction = new Bytecode.ConvertInstruction(
                    instruction.Offset,
                    OperationHelper.ToConvertOperation(instruction.Operation),
                    instruction.ConversionType)
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                body.Instructions.Add(convertInstruction);
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                if (protectedBlocks.Count > 0)
                {
                    var exceptionBlockBuilder = protectedBlocks.Peek();
                    if (exceptionBlockBuilder.StillOnTrySection())
                    {
                        exceptionBlockBuilder.TryEnd = instruction.Offset;
                    }
                    else // rethrow
                    {
                        //TODO


                        // exceptionBlockBuilder.ExceptionType = null; // FIXME ? al ser un rethrow, ya esta seteado esto por un anterior throw?
                        // fixme sin embargo en la rama del if, no habria que setear la excepcion?

                        //             exceptionBlockBuilder = protectedBlocks.Pop();
                        //           exceptionBlockBuilder.HandlerEnd = instruction.Offset;
                    }
                }
                else // not used to exit protected block
                {
                    var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw);
                    body.Instructions.Add(basicInstruction);
                }
            }

            // todo emprolijar
            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                switch (instruction.Operation)
                {
                    // FIXME leave can be used for another purpose than exiting a protected region?
                    case UnconditionalBranchOperation.Leave when protectedBlocks.Count == 0:
                    case UnconditionalBranchOperation.Branch:
                    {
                        var unconditionalBranchInstruction = new Bytecode.BranchInstruction(
                            instruction.Offset,
                            Bytecode.BranchOperation.Branch,
                            Convert.ToUInt32(instruction.Target.Substring(2), 16));
                        body.Instructions.Add(unconditionalBranchInstruction);
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        var exceptionBlockBuilder = protectedBlocks.Pop(); // no more handlers after finally
                        exceptionBlockBuilder.AssertValidHandlerCount();
                        exceptionBlockBuilder
                            .Handlers
                            .Last()
                            .HandlerEnd = instruction.Offset;
                        body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing since filter area is the gap between try end and handler start
                        break;
                    }
                    case UnconditionalBranchOperation.Leave:
                    {
                        var exceptionBlockBuilder = protectedBlocks.Peek();
                        if (exceptionBlockBuilder.StillOnTrySection())
                        {
                            exceptionBlockBuilder.TryEnd = instruction.Offset;
                        }
                        else
                        {
                            if (exceptionBlockBuilder.AllHandlersAdded())
                            {
                                // FIXME codigo repetido con el endfinally. 
                                exceptionBlockBuilder = protectedBlocks.Pop();
                                exceptionBlockBuilder
                                    .Handlers
                                    .Last()
                                    .HandlerEnd = instruction.Offset;
                                body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                            }
                            else
                            {
                                exceptionBlockBuilder
                                    .Handlers
                                    .Last()
                                    .HandlerEnd = instruction.Offset;
                            }
                        }

                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var conditionalBranchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
                    Convert.ToUInt32(instruction.Target.Substring(2), 16));
                body.Instructions.Add(conditionalBranchInstruction);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets.Select(target => Convert.ToUInt32(target.Substring(2), 16)).ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(instruction.Offset, targets);
                body.Instructions.Add(switchInstruction);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(instruction.Offset, instruction.MeasuredType);
                body.Instructions.Add(sizeofInstruction);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(instruction.Offset, instruction.Token);
                body.Instructions.Add(loadTokenInstruction);
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCallInstruction = new Bytecode.MethodCallInstruction(
                    instruction.Offset,
                    OperationHelper.ToMethodCallOperation(instruction.Operation),
                    instruction.Method
                );
                body.Instructions.Add(methodCallInstruction);
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(instruction.Offset, instruction.Function);
                body.Instructions.Add(indirectMethodCallInstruction);
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var createObjectInstruction = new Bytecode.CreateObjectInstruction(instruction.Offset, instruction.Constructor);
                body.Instructions.Add(createObjectInstruction);
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyBlock);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LocalAllocation);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.InitBlock);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(instruction.Offset, instruction.TargetAddress.Type);
                body.Instructions.Add(initObjInstruction);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyObject);
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var createArrayInstruction =
                    new Bytecode.CreateArrayInstruction(instruction.Offset, new ArrayType(instruction.ElementType, instruction.Rank))
                    {
                        WithLowerBound = instruction.LowerBounds.Any()
                    };
                body.Instructions.Add(createArrayInstruction);
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new Exception();
            }

            //TODO en estos builder: validar que las cosas no se seteen mas de una vez, encapsular en metodos que sean mas declarativos, emprolijar, etc
            // ver quiza se mover esto a otro archivo. 

            // TODO ver de extraer algun lugar esto de hacer que las properties se seteen una sola vez. Ya lo use tmb en el metadata container al menos

            // FIXME los try puede tener multiples catch/filter. En el bytecode esto se traduce a mutiples entradas en el exception information
            // FIXME que tienen todas el mismo try pero distinto catch
            // FIXME en el tac pasa lo mismo, es decir tengo instrucciones try con labels repetidas
            private class ProtectedBlockBuilder
            {
                private uint? tryStart;

                public uint TryStart
                {
                    get => tryStart ?? throw new Exception("TryStart was not set");
                    set
                    {
                        if (tryStart != null) throw new Exception("TryStart was already set");
                        tryStart = value;
                    }
                }

                private uint? tryEnd;

                public uint TryEnd
                {
                    get => tryEnd ?? throw new Exception("TryEnd was not set");
                    set
                    {
                        if (tryEnd != null) throw new Exception("TryEnd was already set");
                        tryEnd = value;
                    }
                }

                public bool StillOnTrySection() => tryEnd == null;

                public uint HandlerCount { get; set; }

                public void AssertValidHandlerCount()
                {
                    if (HandlerCount != Handlers.Count) throw new Exception("Expected and actual handler count does not match");
                }

                // FIXME naming
                public bool AllHandlersAdded() => HandlerCount == Handlers.Count;

                public IList<ExceptionHandlerBlockBuilder> Handlers = new List<ExceptionHandlerBlockBuilder>();
                //FIXME revisar si esto esta bien y como uso last(). Asumo que siempre el ultimo es el que quiero. Creo que tiene sentido
                // FIXME por ejemplo en el FIlterInstruction cuando es handler estoy asumiendo que el last es un filter ya

                public IList<ProtectedBlock> Build() =>
                    Handlers
                        .Select(handlerBuilder => handlerBuilder.Build())
                        .Select(handler => new ProtectedBlock(TryStart, TryEnd) {Handler = handler})
                        .ToList();
            }

            private class ExceptionHandlerBlockBuilder
            {
                private uint? filterStart;

                public uint FilterStart
                {
                    get => filterStart ?? throw new Exception("FilterStart was not set");
                    set
                    {
                        if (filterStart != null) throw new Exception("FilterStart was already set");
                        filterStart = value;
                    }
                }

                private uint? handlerStart;

                public uint HandlerStart
                {
                    get => handlerStart ?? throw new Exception("HandlerStart was not set");
                    set
                    {
                        if (handlerStart != null) throw new Exception("HandlerStart was already set");
                        handlerStart = value;
                    }
                }

                private uint? handlerEnd;

                public uint HandlerEnd
                {
                    get => handlerEnd ?? throw new Exception("HandlerEnd was not set");
                    set
                    {
                        if (handlerEnd != null) throw new Exception("HandlerEnd was already set");
                        handlerEnd = value;
                    }
                }

                private ExceptionHandlerBlockKind? handlerBlockKind;

                public ExceptionHandlerBlockKind HandlerBlockKind
                {
                    get => handlerBlockKind ?? throw new Exception("HandlerBlockKind was not set");
                    set
                    {
                        if (handlerBlockKind != null) throw new Exception("HandlerBlockKind was already set");
                        handlerBlockKind = value;
                    }
                }

                private IType exceptionType;

                public IType ExceptionType
                {
                    get => exceptionType ?? throw new Exception("ExceptionType was not set");
                    set
                    {
                        if (exceptionType != null) throw new Exception("ExceptionType was already set");
                        exceptionType = value;
                    }
                }

                public IExceptionHandler Build()
                {
                    switch (HandlerBlockKind)
                    {
                        case ExceptionHandlerBlockKind.Filter:
                            return new FilterExceptionHandler(FilterStart, HandlerStart, HandlerEnd, ExceptionType);
                        case ExceptionHandlerBlockKind.Catch:
                            return new CatchExceptionHandler(HandlerStart, HandlerEnd, ExceptionType);
                        case ExceptionHandlerBlockKind.Fault:
                            return new FaultExceptionHandler(HandlerStart, HandlerEnd);
                        case ExceptionHandlerBlockKind.Finally:
                            return new FinallyExceptionHandler(HandlerStart, HandlerEnd);
                        default: throw new UnknownValueException<ExceptionHandlerBlockKind>(HandlerBlockKind);
                    }
                }
            }
        }
    }
}