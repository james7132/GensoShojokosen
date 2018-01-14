﻿using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HouraiTeahouse.FantasyCrescendo {

public class CharacterRespawn : MonoBehaviour, IPlayerView {

  public Vector3 Offset;

  GameObject platform;

  public Task Initialize(PlayerConfig config, bool isView = false) {
    if (isView) {
      var prefab = Config.Get<VisualConfig>().RespawnPlatformPrefab;
      if (prefab != null) {
        platform = Instantiate(prefab);
        platform.name = prefab.name;
        platform.transform.parent = transform;
        platform.transform.localPosition = Offset;
      }
    }
    return Task.CompletedTask;
  }

  public void ApplyState(PlayerState state) {
    if (platform == null) return;
    platform.SetActive(state.RespawnTimeRemaining > 0);
  }

}

}
