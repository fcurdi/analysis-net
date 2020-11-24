// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using CCIProvider;
using MetadataProvider;
using Model;
using Model.Types;
using Backend.Analyses;
using Backend.Serialization;
using Backend.Transformations;
using Backend.Utils;
using Backend.Model;
using Backend.Transformations.Assembly;
using Model.ThreeAddressCode.Values;
using Tac = Model.ThreeAddressCode.Instructions;
using Bytecode = Model.Bytecode;

namespace Console
{
	class Program
	{
		private Host host;

		public Program(Host host)
		{
			this.host = host;
		}
		
		public void VisitMethods()
		{
			var allDefinedMethods = from a in host.Assemblies
									from t in a.RootNamespace.GetAllTypes()
									from m in t.Members.OfType<MethodDefinition>()
									where m.HasBody
									select m;

			foreach (var method in allDefinedMethods)
			{
				VisitMethod(method);
			}
		}

		private void VisitMethod(MethodDefinition method)
		{
			System.Console.WriteLine(method.ToSignatureString());

			var methodBodyBytecode = method.Body;
			var disassembler = new Disassembler(method);
			var methodBody = disassembler.Execute();			
			method.Body = methodBody;

			var cfAnalysis = new ControlFlowAnalysis(method.Body);
			//var cfg = cfAnalysis.GenerateNormalControlFlow();
			var cfg = cfAnalysis.GenerateExceptionalControlFlow();

			var dgml_CFG = DGMLSerializer.Serialize(cfg);

			var domAnalysis = new DominanceAnalysis(cfg);
			domAnalysis.Analyze();
			domAnalysis.GenerateDominanceTree();

			var loopAnalysis = new NaturalLoopAnalysis(cfg);
			loopAnalysis.Analyze();

			var domFrontierAnalysis = new DominanceFrontierAnalysis(cfg);
			domFrontierAnalysis.Analyze();

			var splitter = new WebAnalysis(cfg);
			splitter.Analyze();
			splitter.Transform();

			methodBody.UpdateVariables();

			var typeAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
			typeAnalysis.Analyze();

			// Copy Propagation
			var forwardCopyAnalysis = new ForwardCopyPropagationAnalysis(cfg);
			forwardCopyAnalysis.Analyze();
			forwardCopyAnalysis.Transform(methodBody);

			var backwardCopyAnalysis = new BackwardCopyPropagationAnalysis(cfg);
			backwardCopyAnalysis.Analyze();
			backwardCopyAnalysis.Transform(methodBody);

			// Points-To
			var pointsTo = new PointsToAnalysis(cfg, method);
			var result = pointsTo.Analyze();

			var ptg = result[cfg.Exit.Id].Output;
			//ptg.RemoveVariablesExceptParameters();
			ptg.RemoveTemporalVariables();

			var dgml_PTG = DGMLSerializer.Serialize(ptg);

			// Live Variables
			var liveVariables = new LiveVariablesAnalysis(cfg);
			var livenessInfo = liveVariables.Analyze();

			// SSA
			var ssa = new StaticSingleAssignment(methodBody, cfg);
			ssa.Transform();
			ssa.Prune(livenessInfo);

			methodBody.UpdateVariables();

			//var dot = DOTSerializer.Serialize(cfg);
			//var dgml = DGMLSerializer.Serialize(cfg);

			//dgml = DGMLSerializer.Serialize(host, typeDefinition);
		}

		private static void RunSomeTests()
		{
			const string root = @"..\..\..";
			//const string root = @"C:"; // casa
			//const string root = @"C:\Users\Edgar\Projects"; // facu

			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();
			//host.Assemblies.Add(assembly);

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var type = new BasicType("ExamplesPointsTo")
			{
				ContainingAssembly = new AssemblyReference("Test"),
				ContainingNamespace = "Test"
			};

			var typeDefinition = host.ResolveReference(type);

			var method = new MethodReference("Example1", PlatformTypes.Void)
			{
				ContainingType = type,
			};

			//var methodDefinition = host.ResolveReference(method) as MethodDefinition;

			var program = new Program(host);
			program.VisitMethods();

			// Testing method calls inlining
			var methodDefinition = host.ResolveReference(method) as MethodDefinition;
			var methodCalls = methodDefinition.Body.Instructions.OfType<Tac.MethodCallInstruction>().ToList();

			foreach (var methodCall in methodCalls)
			{
				var callee = host.ResolveReference(methodCall.Method) as MethodDefinition;
				methodDefinition.Body.Inline(methodCall, callee.Body);
			}

			methodDefinition.Body.UpdateVariables();

			type = new BasicType("ExamplesCallGraph")
			{
				ContainingAssembly = new AssemblyReference("Test"),
				ContainingNamespace = "Test"
			};

			method = new MethodReference("Example1", PlatformTypes.Void)
			{
				ContainingType = type,
			};

			methodDefinition = host.ResolveReference(method) as MethodDefinition;

			var ch = new ClassHierarchy();
			ch.Analyze(host);

			var dgml = DGMLSerializer.Serialize(ch);

			var chcga = new ClassHierarchyAnalysis(ch);
			var roots = host.GetRootMethods();
			var cg = chcga.Analyze(host, roots);

			dgml = DGMLSerializer.Serialize(cg);
		}

		private static void RunGenericsTests()
		{
			const string root = @"..\..\..";
			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var assembly = new AssemblyReference("Test");

			var typeA = new GenericParameterReference(GenericParameterKind.Type, 0);
			var typeB = new GenericParameterReference(GenericParameterKind.Type, 1);

			var typeNestedClass = new BasicType("NestedClass")
			{
				ContainingAssembly = assembly,
				ContainingNamespace = "Test",
				GenericParameterCount = 2,
				ContainingType = new BasicType("ExamplesGenerics")
				{
					ContainingAssembly = assembly,
					ContainingNamespace = "Test",
					GenericParameterCount = 1
				}				
			};

			//typeNestedClass.ContainingType.GenericArguments.Add(typeA);
			//typeNestedClass.GenericArguments.Add(typeB);

			var typeDefinition = host.ResolveReference(typeNestedClass);

			if (typeDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve type:\n{0}", typeNestedClass);
			}

			var typeK = new GenericParameterReference(GenericParameterKind.Method, 0);
			var typeV = new GenericParameterReference(GenericParameterKind.Method, 1);

			var typeKeyValuePair = new BasicType("KeyValuePair")
			{
				ContainingAssembly = new AssemblyReference("mscorlib"),
				ContainingNamespace = "System.Collections.Generic",
				GenericParameterCount = 2
			};

			typeKeyValuePair.GenericArguments.Add(typeK);
			typeKeyValuePair.GenericArguments.Add(typeV);

			var methodExampleGenericMethod = new MethodReference("ExampleGenericMethod", typeKeyValuePair)
			{
				ContainingType = typeNestedClass,
				GenericParameterCount = 2
			};

			//methodExampleGenericMethod.GenericArguments.Add(typeK);
			//methodExampleGenericMethod.GenericArguments.Add(typeV);

			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(0, typeA));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(1, typeB));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(2, typeK));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(3, typeV));
			methodExampleGenericMethod.Parameters.Add(new MethodParameterReference(4, typeKeyValuePair));

			var methodDefinition = host.ResolveReference(methodExampleGenericMethod) as MethodDefinition;

			if (methodDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", methodExampleGenericMethod);
			}

			var methodExample = new MethodReference("Example", PlatformTypes.Void)
			{
				ContainingType = new BasicType("ExamplesGenericReferences")
				{
					ContainingAssembly = assembly,
					ContainingNamespace = "Test"
				}
			};

			methodDefinition = host.ResolveReference(methodExample) as MethodDefinition;

			if (methodDefinition == null)
			{
				System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", methodExample);
			}

			var calls = methodDefinition.Body.Instructions.OfType<Bytecode.MethodCallInstruction>();

			foreach (var call in calls)
			{
				methodDefinition = host.ResolveReference(call.Method) as MethodDefinition;

				if (methodDefinition == null)
				{
					System.Console.WriteLine("[Error] Cannot resolve method:\n{0}", call.Method);
				}
			}
		}

		private static void RunInterPointsToTests()
		{
			const string root = @"..\..\..";
			const string input = root + @"\Test\bin\Debug\Test.dll";

			var host = new Host();

			PlatformTypes.Resolve(host);

			var loader = new Loader(host);
			loader.LoadAssembly(input);
			//loader.LoadCoreAssembly();

			var methodReference = new MethodReference("Example6", PlatformTypes.Void)
			//var methodReference = new MethodReference("Example6", PlatformTypes.Void)
			//var methodReference = new MethodReference("ExampleDelegateCaller", PlatformTypes.Void)
			{
				ContainingType = new BasicType("ExamplesPointsTo", TypeKind.ReferenceType)
				{
					ContainingAssembly = new AssemblyReference("Test"),
					ContainingNamespace = "Test"
				}
			};

			//var parameter = new MethodParameterReference(0, PlatformTypes.Boolean);
			//methodReference.Parameters.Add(parameter);
			//parameter = new MethodParameterReference(1, PlatformTypes.Boolean);
			//methodReference.Parameters.Add(parameter);

			//methodReference.ReturnType = new BasicType("Node", TypeKind.ReferenceType)
			//{
			//	ContainingAssembly = new AssemblyReference("Test"),
			//	ContainingNamespace = "Test"
			//};

			methodReference.Resolve(host);

			var programInfo = new ProgramAnalysisInfo();
			var pta = new InterPointsToAnalysis(programInfo);

			var cg = pta.Analyze(methodReference.ResolvedMethod);
			var dgml_CG = DGMLSerializer.Serialize(cg);

			//System.IO.File.WriteAllText(@"cg.dgml", dgml_CG);

			var esca = new EscapeAnalysis(programInfo, cg);
			var escapeResult = esca.Analyze();

			var fea = new FieldEffectsAnalysis(programInfo, cg);
			var effectsResult = fea.Analyze();

			foreach (var method in cg.Methods)
			{
				MethodAnalysisInfo methodInfo;
				var ok = programInfo.TryGet(method, out methodInfo);
				if (!ok) continue;

				InterPointsToInfo pti;
				ok = methodInfo.TryGet(InterPointsToAnalysis.INFO_IPTA_RESULT, out pti);

				if (ok)
				{
					var ptg = pti.Output;
					ptg.RemoveTemporalVariables();
					//ptg.RemoveVariablesExceptParameters();
					var dgml_PTG = DGMLSerializer.Serialize(ptg);

					//System.IO.File.WriteAllText(@"ptg.dgml", dgml_PTG);
				}

				EscapeInfo escapeInfo;
				ok = escapeResult.TryGetValue(method, out escapeInfo);

				FieldEffectsInfo effectsInfo;
				ok = effectsResult.TryGetValue(method, out effectsInfo);
			}
		}

		private static void ProgramaticallyGeneratedAssembly()
		{
			var mscorlib = new AssemblyReference("mscorlib")
			{
				Version = new Version("4.0.0.0"),
				Culture = "",
				PublicKey = new byte[0]
			};
			var assembly = new Assembly("ProgramaticallyGeneratedAssembly", AssemblyKind.Exe)
			{
				Version = new Version("1.0.0.0"),
				PublicKey = new byte[0],
				Culture = "",
				References = {mscorlib}
			};
			var rootNamespace = new Namespace("") {ContainingAssembly = assembly};
			rootNamespace.Types.Add(new TypeDefinition("<Module>", TypeKind.ReferenceType, TypeDefinitionKind.Class)
			{
				ContainingAssembly = assembly,
				ContainingNamespace = rootNamespace,
				Visibility = VisibilityKind.Private
			});
			assembly.RootNamespace = rootNamespace;

			var objectType = new BasicType("Object", TypeKind.ReferenceType)
			{
				ContainingAssembly = mscorlib,
				ContainingNamespace = "System",
			};
			var objectConstructorMethod = new MethodReference(".ctor", PlatformTypes.Void)
			{
				ContainingType = objectType
			};

			var languagesNamespace = new Namespace("Languages") {ContainingAssembly = assembly, ContainingNamespace = rootNamespace};
			rootNamespace.Namespaces.Add(languagesNamespace);
			var englishType = new TypeDefinition("English", TypeKind.ReferenceType, TypeDefinitionKind.Class)
			{
				ContainingAssembly = assembly,
				ContainingNamespace = languagesNamespace,
				Visibility = VisibilityKind.Public,
				IsStatic = false,
				Base = objectType
			};
			var englishGreetMethod = new MethodDefinition("Greet", PlatformTypes.String)
			{
				Visibility = VisibilityKind.Public,
				ContainingType = englishType,
				IsStatic = false,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					Instructions =
					{
						new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Value, new Constant("¡Hello!")),
						new Bytecode.BasicInstruction(1, Bytecode.BasicOperation.Return)
					}
				}
			};

			var thisVariable = new LocalVariable("this", true);
			var englishTypeConstructor = new MethodDefinition(".ctor", PlatformTypes.Void)
			{
				SpecialName = true,
				RuntimeSpecialName = true,
				Visibility = VisibilityKind.Public,
				ContainingType = englishType,
				IsStatic = false,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					Parameters = {thisVariable},
					Instructions =
					{
						new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Content, thisVariable),
						new Bytecode.MethodCallInstruction(1, Bytecode.MethodCallOperation.Static, objectConstructorMethod),
						new Bytecode.BasicInstruction(2, Bytecode.BasicOperation.Nop),
						new Bytecode.BasicInstruction(3, Bytecode.BasicOperation.Return)
					}
				}
			};
			englishType.Methods.Add(englishTypeConstructor);
			englishType.Methods.Add(englishGreetMethod);
			languagesNamespace.Types.Add(englishType);

			var spanishType = new TypeDefinition("Spanish", TypeKind.ReferenceType, TypeDefinitionKind.Class)
			{
				ContainingAssembly = assembly,
				ContainingNamespace = languagesNamespace,
				Visibility = VisibilityKind.Public,
				IsStatic = false,
				Base = objectType
			};
			var spanishGreetMethod = new MethodDefinition("Greet", PlatformTypes.String)
			{
				Visibility = VisibilityKind.Public,
				ContainingType = spanishType,
				IsStatic = false,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					Instructions =
					{
						new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Value, new Constant("¡Hola!")),
						new Bytecode.BasicInstruction(1, Bytecode.BasicOperation.Return)
					}
				}
			};
			var spanishTypeConstructor = new MethodDefinition(".ctor", PlatformTypes.Void)
			{
				SpecialName = true,
				RuntimeSpecialName = true,
				Visibility = VisibilityKind.Public,
				ContainingType = spanishType,
				IsStatic = false,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					Parameters = {thisVariable},
					Instructions =
					{
						new Bytecode.LoadInstruction(0, Bytecode.LoadOperation.Content, thisVariable),
						new Bytecode.MethodCallInstruction(1, Bytecode.MethodCallOperation.Static, objectConstructorMethod),
						new Bytecode.BasicInstruction(2, Bytecode.BasicOperation.Nop),
						new Bytecode.BasicInstruction(3, Bytecode.BasicOperation.Return)
					}
				}
			};
			spanishType.Methods.Add(spanishGreetMethod);
			spanishType.Methods.Add(spanishTypeConstructor);
			languagesNamespace.Types.Add(spanishType);

			var consoleNamespace = new Namespace("Console") {ContainingAssembly = assembly, ContainingNamespace = rootNamespace};
			rootNamespace.Namespaces.Add(consoleNamespace);
			var programType = new TypeDefinition("Program", TypeKind.ReferenceType, TypeDefinitionKind.Class)
			{
				ContainingAssembly = assembly,
				ContainingNamespace = consoleNamespace,
				Visibility = VisibilityKind.Public,
				IsStatic = true,
				Base = objectType
			};
			consoleNamespace.Types.Add(programType);

			var mainMethod = new MethodDefinition("Main", PlatformTypes.Void)
			{
				Visibility = VisibilityKind.Public,
				ContainingType = programType,
				IsStatic = true,
				Body = new MethodBody(MethodBodyKind.Bytecode)
				{
					Instructions =
					{
						new Bytecode.BasicInstruction(0, Bytecode.BasicOperation.Nop),
						new Bytecode.CreateObjectInstruction(1, englishTypeConstructor),
						new Bytecode.MethodCallInstruction(2, Bytecode.MethodCallOperation.Static, englishGreetMethod),
						new Bytecode.MethodCallInstruction(3, Bytecode.MethodCallOperation.Static, ConsoleWriteLineMethodReference()),
						new Bytecode.BasicInstruction(4, Bytecode.BasicOperation.Nop),
						new Bytecode.CreateObjectInstruction(5, spanishTypeConstructor),
						new Bytecode.MethodCallInstruction(6, Bytecode.MethodCallOperation.Static, spanishGreetMethod),
						new Bytecode.MethodCallInstruction(7, Bytecode.MethodCallOperation.Static, ConsoleWriteLineMethodReference()),
						new Bytecode.BasicInstruction(8, Bytecode.BasicOperation.Nop),
						new Bytecode.BasicInstruction(9, Bytecode.BasicOperation.Return)
					}
				}
			};
			programType.Methods.Add(mainMethod);

			var generator = new MetadataGenerator.Generator();
			generator.Generate(assembly);
		}

		// Each method body is translated to TAC and modified by adding a log at the beginning of the method. Then it is translated back
		// to Bytecode and generated
		private static void TacInstrumentation() => DoWithLibs(input =>
		{
			var host = new Host();

			PlatformTypes.Resolve(host);

			System.Console.WriteLine($"Reading {input}");
			var loader = new MetadataProvider.Loader(host);
			loader.LoadAssembly(input);

			var allDefinedMethods = (from a in host.Assemblies
				from t in a.RootNamespace.GetAllTypes()
				from m in t.Members.OfType<MethodDefinition>()
				where m.HasBody
				select m).ToList();
			
			foreach (var method in allDefinedMethods)
			{
				var tac = new Backend.Transformations.Disassembler(method).Execute();
				method.Body = tac;

				AddLogAtMethodEntry($"Entering method: {method.Name} of class: {method.ContainingType.Name}", method.Body);

				var cfanalysis = new ControlFlowAnalysis(method.Body);
				var cfg = cfanalysis.GenerateExceptionalControlFlow();

				var webAnalysis = new WebAnalysis(cfg);
				webAnalysis.Analyze();
				webAnalysis.Transform();
				method.Body.UpdateVariables();

				var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
				typeInferenceAnalysis.Analyze();

				var bytecode = new Backend.Transformations.Assembly.Assembler(method).Execute();
				method.Body = bytecode;
			}

			var generator = new MetadataGenerator.Generator();

			foreach (var assembly in host.Assemblies)
			{
				generator.Generate(assembly);
			}
		});

		private static void AddLogAtMethodEntry(string message, MethodBody body)
		{
			var result = new TemporalVariable("$s", 0);
			var loadInstruction = new Tac.LoadInstruction(0, result, new Constant(message))
			{
				Label = body.Instructions.First().Label + "º"
			};
			var methodCallInstruction = new Tac.MethodCallInstruction(
				0,
				null,
				Tac.MethodCallOperation.Static,
				ConsoleWriteLineMethodReference(),
				new List<IVariable> {result})
			{
				Label = loadInstruction.Label + "º"
			};

			var instructions = new List<Tac.Instruction> {loadInstruction, methodCallInstruction}
				.Concat(body.Instructions)
				.ToList();

			body.Instructions.Clear();
			body.Instructions.AddRange(instructions);
		}

		private static MethodReference ConsoleWriteLineMethodReference()
		{
			var writeLineMethod = new MethodReference("WriteLine", PlatformTypes.Void)
			{
				Parameters = {new MethodParameter(0, "value", PlatformTypes.String)},
				IsStatic = true
			};
			var consoleType = new BasicType("Console", TypeKind.ReferenceType)
			{
				ContainingAssembly = new AssemblyReference("mscorlib") {Version = new Version("4.0.0.0")},
				ContainingNamespace = "System"
			};
			writeLineMethod.ContainingType = consoleType;
			return writeLineMethod;
		}

		private static void ReadAndGenerateDll(bool transformToTacAndBackToBytecode) => DoWithLibs(file =>
		{
			var host = new Host();

			PlatformTypes.Resolve(host);

			System.Console.WriteLine($"Reading {file}");
			var loader = new MetadataProvider.Loader(host);
			loader.LoadAssembly(file);

			if (transformToTacAndBackToBytecode)
			{
				var allDefinedMethods = (from a in host.Assemblies
					from t in a.RootNamespace.GetAllTypes()
					from m in t.Members.OfType<MethodDefinition>()
					where m.HasBody
					select m).ToList();

				foreach (var method in allDefinedMethods)
				{
					var tac = new Backend.Transformations.Disassembler(method).Execute();
					method.Body = tac;

					var cfanalysis = new ControlFlowAnalysis(method.Body);
					var cfg = cfanalysis.GenerateExceptionalControlFlow();

					var webAnalysis = new WebAnalysis(cfg);
					webAnalysis.Analyze();
					webAnalysis.Transform();
					method.Body.UpdateVariables();

					var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
					typeInferenceAnalysis.Analyze();

					var bytecode = new Backend.Transformations.Assembly.Assembler(method).Execute();
					method.Body = bytecode;
				}
			}

			var generator = new MetadataGenerator.Generator();

			foreach (var assembly in host.Assemblies)
			{
				generator.Generate(assembly);
			}
		});

		// TODO hacerlo generico y ver como darle sentido para los casos de estudio (ya que al ser libs van a tener metodos que no se usan).
		private static void RemoveUnusedMethodFromSimpleExecutable()
		{
			var input = "../../../ExamplesEXE/bin/Debug/ExamplesEXE.exe";

			var host = new Host();

			PlatformTypes.Resolve(host);

			System.Console.WriteLine($"Reading {input}");
			var loader = new MetadataProvider.Loader(host);
			loader.LoadAssembly(input);
			
			var allDefinedMethods = (from a in host.Assemblies
				from t in a.RootNamespace.GetAllTypes()
				from m in t.Members.OfType<MethodDefinition>()
				select m).ToList();

			// convert bodies to typed tac
			foreach (var method in allDefinedMethods)
			{
				var tac = new Backend.Transformations.Disassembler(method).Execute();
				method.Body = tac;
				
				var cfanalysis = new ControlFlowAnalysis(method.Body);
				var cfg = cfanalysis.GenerateExceptionalControlFlow();

				var webAnalysis = new WebAnalysis(cfg);
				webAnalysis.Analyze();
				webAnalysis.Transform();
				method.Body.UpdateVariables();

				var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
				typeInferenceAnalysis.Analyze();
			}
			
			// run call graph analysis
			var classHierarchy = new ClassHierarchy();
			classHierarchy.Analyze(host);
			var classHierarchyAnalysis = new ClassHierarchyAnalysis(classHierarchy);
			var roots = host.GetRootMethods();
			var callGraph = classHierarchyAnalysis.Analyze(host, roots);

			// convert back to bytecode for generation
			foreach (var method in allDefinedMethods)
			{
				var bytecode = new Backend.Transformations.Assembly.Assembler(method).Execute();
				method.Body = bytecode;
			}
			
			// remove unused method
			var unusedPrivateMethods = allDefinedMethods
				.Except(callGraph.Methods.Cast<MethodDefinition>())
				.Where(method => method.Visibility == VisibilityKind.Private)
				.ToList();
			foreach (var unusedMethods in unusedPrivateMethods)
			{
				unusedMethods.ContainingType.Methods.Remove(unusedMethods);
			}
			
			System.Console.WriteLine($"File: {input} - Unused methods removed: {unusedPrivateMethods.Count}");

			// generate
			var generator = new MetadataGenerator.Generator();
			foreach (var assembly in host.Assemblies)
			{
				generator.Generate(assembly);
			}
		}

		private static void DoWithLibs(Action<string> operation)
		{
			var inputs = new[]
			{
				new[] {"../../../Examples/bin/Debug/Examples.dll"},
				new[]
				{
					"../../../../TinyCsvParser/TinyCsvParser/TinyCsvParser/bin/Debug/net45/TinyCsvParser.dll",
					"../../../../TinyCsvParser/TinyCsvParser/TinyCsvParser.Test/bin/Debug/net45/TinyCsvParser.Test.dll"
				},
				new[]
				{
					"../../../../DSA/DSA/DSA/bin/Debug/net45/DSA.dll",
					"../../../../DSA/DSA/DSAUnitTests/bin/Debug/net45/DSAUnitTests.dll"
				},
				new[]
				{
					"../../../../Fleck/src/Fleck.Tests/bin/Debug/net45/Fleck.dll",
					"../../../../Fleck/src/Fleck.Tests/bin/Debug/net45/Fleck.Tests.dll"
				},
				new[]
				{
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Async.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Tests.dll",
					"../../../../Optional/src/Optional.Tests/bin/Debug/net45/Optional.Utilities.dll"
				}
			};

			foreach (var file in inputs.SelectMany(i => i))
			{
				operation.Invoke(file);
			}
			
		}

		static void Main(string[] args)
		{
		// generation
			 ReadAndGenerateDll(transformToTacAndBackToBytecode: false); 
			// ReadAndGenerateDll(transformToTacAndBackToBytecode: true);
		//
		
		// instrumentation
		//	TacInstrumentation();
			// TODO instrument tests by printing the name of the test running.
		//
		
		// programmatic generation
			// ProgramaticallyGeneratedAssembly();
		//
		
		// optimization
			// RemoveUnusedMethodFromSimpleExecutable();
		//	

			System.Console.WriteLine("Done!");
		}
	}
}