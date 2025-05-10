using UnityEngine;

using System.Collections.Generic;
using System.Linq;

using Linda.Plants;

namespace Linda
{
	public class Plant : MonoBehaviour
	{
		[SerializeField] string axiom;
		[SerializeField] string[] variables = new string[] { "L", "fwd", "rot" };
		[SerializeField] string[] functions = new string[] { "randrange" };
		[SerializeField] char[] constants = new char[] { '[', ']' };

		[System.Serializable]
		class RulePair
		{
			public string Key = null!;
			public string Value = null!;
		}
		[SerializeField] List<RulePair> rules;
		[SerializeField] int iterations;

		LSystem mLSystem;
		int prevIterations;

		void Start()
		{
			mLSystem = new LSystem(variables, functions, constants, new Behavior(transform));

			prevIterations = 0;
		}

		void Update()
		{
			if (iterations != prevIterations)
			{
				foreach (Transform child in transform)
				{
					Destroy(child.gameObject);
				}

				List<Token> expr = mLSystem.Build(axiom, rules.Select(x => (x.Key, x.Value)).ToList(), iterations);
				mLSystem.Execute(expr);

				prevIterations = iterations;
			}
		}
	}
}