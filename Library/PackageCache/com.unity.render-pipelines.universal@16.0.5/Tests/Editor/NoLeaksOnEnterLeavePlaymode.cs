using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.Rendering.Universal.Internal;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

class NoLeaksOnEnterLeavePlaymode
{
    // This variable needs to be serialized, as going into play-mode will reload the domain and
    // wipe the state of all objects, including the content of this variable
    [SerializeField]
    string materialNames = "";
    [SerializeField]
    string meshNames = "";
    [SerializeField]
    string textureNames = "";

    void EnsureUniversalRPIsActivePipeline()
    {
        Camera.main.Render();

        // Skip test if project is not configured to be SRP project
        if (RenderPipelineManager.currentPipeline == null)
            Assert.Ignore("Test project has no SRP configured, skipping test");

        Assert.IsInstanceOf<UniversalRenderPipeline>(RenderPipelineManager.currentPipeline);
    }

    Dictionary<string, int> CountResources(string[] names)
    {
        var result = new Dictionary<string, int>();
        foreach (string name in names)
        {
            if (result.TryGetValue(name, out int materialCount))
            {
                result[name] = materialCount + 1;
            }
            else
            {
                result.Add(name, 1);
            }
        }
        return result;
    }

    void CompareResourceLists(Dictionary<string, int> oldList, Dictionary<string, int> newList, string [] blackList)
    {
        foreach (var newRes in newList)
        {
            // Ignore blacklisted materials
            if (blackList.Contains(newRes.Key)) continue;

            int oldCount = 0;
            oldList.TryGetValue(newRes.Key, out oldCount);
            if (newRes.Value > oldCount)
            {
                Debug.LogError("Leaked " + newRes.Key + "(" + (newRes.Value - oldCount) + "x)");
            }
        }
    }

    [UnityTest]
    public IEnumerator NoResourceLeaks()
    {
        // give it a chance to warm-up by entering play mode once
        // in theory this shouldn't be needed but I hope this avoids the worst instabilities.
        yield return new EnterPlayMode();
        yield return null;
        yield return new ExitPlayMode();
        yield return null;

        // Grab the list of existing objects
        var mats = Resources.FindObjectsOfTypeAll(typeof(Material));
        materialNames = string.Join(";", mats.Select(m => m.name));

        var meshes = Resources.FindObjectsOfTypeAll(typeof(Mesh));
        meshNames = string.Join(";", meshes.Select(m => m.name));

        var textures = Resources.FindObjectsOfTypeAll(typeof(Texture));
        textureNames = string.Join(";", textures.Select(m => m.name));

        yield return new EnterPlayMode();
        yield return null;
        yield return new ExitPlayMode();
        yield return null;

        // Grab lists of existing objects.
        // Note: resources created from code often reuse the same names so we have to both check the names
        // and the counts per name to ensure nothing leaked.
        var newMats = Resources.FindObjectsOfTypeAll(typeof(Material));
        var newMeshes = Resources.FindObjectsOfTypeAll(typeof(Mesh));
        var newTextures = Resources.FindObjectsOfTypeAll(typeof(Texture));

        string[] materialBlackList = {
            "Hidden/Universal/HDRDebugView",
            "Hidden/Universal Render Pipeline/Debug/DebugReplacement",
            "Inter - Regular Material" //https://jira.unity3d.com/browse/UUM-28555
        };

        var oldMaterialNames = materialNames.Split(";");
        var materialsPerNameOld = CountResources(oldMaterialNames);
        var newMaterialNames = newMats.Select(m => m.name).ToArray();
        var materialsPerNameNew = CountResources(newMaterialNames);
        CompareResourceLists(materialsPerNameOld, materialsPerNameNew, materialBlackList);

        string[] meshBlackList = {
        };

        var oldMeshNames = meshNames.Split(";");
        var meshesPerNameOld = CountResources(oldMeshNames);
        var newMeshNames = newMeshes.Select(m => m.name).ToArray();
        var meshesPerNameNew = CountResources(newMeshNames);
        CompareResourceLists(meshesPerNameOld, meshesPerNameNew, meshBlackList);

        string[] textureBlackList = {
            "Inter - Regular Atlas" // more fonts leakage :-\
        };

        var oldTextureNames = textureNames.Split(";");
        var texturesPerNameOld = CountResources(oldTextureNames);
        var newTextureNames = newTextures.Select(m => m.name).ToArray();
        var texturesPerNameNew = CountResources(newTextureNames);
        CompareResourceLists(texturesPerNameOld, texturesPerNameNew, textureBlackList);
    }
}
