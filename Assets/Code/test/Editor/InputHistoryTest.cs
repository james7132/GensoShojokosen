﻿using HouraiTeahouse;
using HouraiTeahouse.FantasyCrescendo;
using HouraiTeahouse.FantasyCrescendo.Matches;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class InputHistoryTest {

  static IEnumerable<object[]> TestCases() {
    for (var i = 1; i <= GameMode.GlobalMaxPlayers; i++) {
      yield return new object[] { i };
    }
  }

  class DisposableTest : IDisposable, IMergable<DisposableTest> {
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
    public DisposableTest MergeWith(DisposableTest merge) => this;
  }

	[TestCaseSource("TestCases")]
	public void Append_maintains_order(int playerCount) {
    var input1 = InputUtility.RandomInput(playerCount);
    var input2 = InputUtility.RandomInput(playerCount);
    var input3 = InputUtility.RandomInput(playerCount);
    var input4 = InputUtility.RandomInput(playerCount);

    var inputHistory = new InputHistory<MatchInput>();
    inputHistory.Append(input2);
    inputHistory.Append(input3);
    inputHistory.Append(input4);

    var results = new[] { input1, input2, input3, input4 };

    Assert.That(inputHistory.Select(i => i.Input), Is.EqualTo(results));
    AssertHistoryMaintainsOrder(inputHistory);
	}

	[TestCaseSource("TestCases")]
	public void Append_increments_Count(int playerCount) {
    var inputHistory = new InputHistory<MatchInput>();
    Assert.That(inputHistory.Count, Is.EqualTo(1));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Count, Is.EqualTo(2));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Count, Is.EqualTo(3));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Count, Is.EqualTo(4));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Count, Is.EqualTo(5));
    AssertHistoryMaintainsOrder(inputHistory);
	}
  
	[TestCaseSource("TestCases")]
	public void Append_increments_CurrentTimestep(int playerCount) {
    var inputHistory = new InputHistory<MatchInput>();
    Assert.That(inputHistory.Current.Timestep, Is.EqualTo(20));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Current.Timestep, Is.EqualTo(21));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Current.Timestep, Is.EqualTo(22));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Current.Timestep, Is.EqualTo(23));
    inputHistory.Append(InputUtility.RandomInput(playerCount));
    Assert.That(inputHistory.Current.Timestep, Is.EqualTo(24));
    AssertHistoryMaintainsOrder(inputHistory);
	}

	[TestCaseSource("TestCases")]
	public void Append_updates_CurrentInput(int playerCount) {
    var latestInput = InputUtility.RandomInput(playerCount);
    var inputHistory = new InputHistory<MatchInput>();
    Assert.That(inputHistory.Current.Input, Is.EqualTo(latestInput));
    for (var i = 0; i < 20; i++) {
      latestInput = InputUtility.RandomInput(playerCount);
      inputHistory.Append(latestInput);
      Assert.That(inputHistory.Current.Input, Is.EqualTo(latestInput));
    }
    AssertHistoryMaintainsOrder(inputHistory);
	}

	[TestCaseSource("TestCases")]
	public void Append_does_not_update_Oldest(int playerCount) {
    var firstInput = InputUtility.RandomInput(playerCount);
    var inputHistory = new InputHistory<MatchInput>(42);
    Assert.That(inputHistory.Oldest.Timestep, Is.EqualTo(42));
    Assert.That(inputHistory.Oldest.Input, Is.EqualTo(firstInput));
    for (var i = 0; i < 20; i++) {
      inputHistory.Append(InputUtility.RandomInput(playerCount));
      Assert.That(inputHistory.Oldest.Input, Is.EqualTo(firstInput));
      Assert.That(inputHistory.Oldest.Timestep, Is.EqualTo(42));
    }
    AssertHistoryMaintainsOrder(inputHistory);
	}

	[Test]
	public void MergeWith_merges_stored_values() {
    var inputHistory = new InputHistory<int>(0, Merger<int>.FromDelegate((a, b) => a + b));
    inputHistory.Append(1);
    inputHistory.Append(2);
    inputHistory.Append(3);
    inputHistory.Append(4);
    inputHistory.Append(5);
    inputHistory.Append(6);

    inputHistory.MergeWith(2, new ArraySegment<int>(new[] {5, 5, 5, 5}));

    Assert.That(inputHistory.Select(i => i.Input).ToArray(), 
                Is.EqualTo(new[] { 0, 1, 7, 8, 9, 10, 6}));
    AssertHistoryMaintainsOrder(inputHistory);
	}

	[Test]
	public void MergeWith_appends_values_if_over_end_of_history() {
    var inputHistory = new InputHistory<int>(0, Merger<int>.FromDelegate((a, b) => a + b));
    inputHistory.Append(1);
    inputHistory.Append(2);
    inputHistory.Append(3);
    inputHistory.Append(4);
    inputHistory.Append(5);
    inputHistory.Append(6);

    inputHistory.MergeWith(5, new ArraySegment<int>(new[] {5, 5, 5, 5}));

    Assert.That(inputHistory.GetFullSequence().Select(i => i.Input).ToArray(), 
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 10, 11, 5, 5 }));

    Assert.That(inputHistory.Select(i => i.Input).ToArray(), 
                Is.EqualTo(new[] { 0, 1, 2, 3, 4, 10, 11 }));
    AssertHistoryMaintainsOrder(inputHistory);
	}

  void AssertHistoryMaintainsOrder<T>(InputHistory<T> history) where T : struct {
    var lastTimestep = uint.MinValue;
    foreach (var timedInput in history.GetFullSequence()) {
      Assert.That(timedInput.Timestep, Is.GreaterThanOrEqualTo(lastTimestep));
      lastTimestep = timedInput.Timestep;
    }
  }

}
