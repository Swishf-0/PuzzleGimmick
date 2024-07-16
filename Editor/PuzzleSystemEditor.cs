using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

namespace PuzzleSystem
{
    [CustomEditor(typeof(PuzzleSystem))]
    public class PuzzleSystemEditor : Editor
    {
        SerializedProperty _pieceInfoProperty, _movieInfoProperty, _movieInfoTableProperty;
        string _pieceInfoString, _movieInfoString;

        private void OnEnable()
        {
            _pieceInfoProperty = serializedObject.FindProperty("_pieceInfo");
            _movieInfoProperty = serializedObject.FindProperty("_movieInfo");
            _movieInfoTableProperty = serializedObject.FindProperty("_movieInfoTable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("===== [地域情報] シートをコピーしてここに張り付け =====");
                if (GUILayout.Button("変換する"))
                {
                    ConvertRegionInfo(_pieceInfoString);
                }
            }
            EditorGUILayout.EndHorizontal();
            _pieceInfoString = EditorGUILayout.TextArea(_pieceInfoString, GUILayout.MinHeight(50));

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("===== [動画情報] シートをコピーしてここに張り付け =====");
                if (GUILayout.Button("変換する"))
                {
                    ConvertMovieInfo(_movieInfoString);
                }
            }
            EditorGUILayout.EndHorizontal();
            _movieInfoString = EditorGUILayout.TextArea(_movieInfoString, GUILayout.MinHeight(50));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(20);
            DrawDefaultInspector();
        }

        void ConvertRegionInfo(string infoString)
        {
            var res = infoString.Split("\n").Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Replace("\t", ",")).ToArray();
            _pieceInfoProperty.arraySize = res.Length;
            for (int i = 0; i < res.Length; i++)
            {
                _pieceInfoProperty.GetArrayElementAtIndex(i).stringValue = res[i];
            }

            _pieceInfoString = string.Empty;
        }

        void ConvertMovieInfo(string infoString)
        {
            var infos = infoString.Split("\n").Where(x => !string.IsNullOrEmpty(x)).ToArray();
            var titles = new List<string>(infos.Length);
            var infoIds = new List<string>(infos.Length);
            for (int i = 0; i < infos.Length; i++)
            {
                var _data = infos[i].Split("\t");
                titles.Add(_data[0]);
                infoIds.Add(_data[1]);
            }

            var resList = new List<int>[_pieceInfoProperty.arraySize];
            for (int i = 0; i < resList.Length; i++)
            {
                resList[i] = new List<int>();
            }
            for (int i = 0; i < infoIds.Count; i++)
            {
                var idStrings = infoIds[i].Split(",");
                for (int j = 0; j < idStrings.Length; j++)
                {
                    if (int.TryParse(idStrings[j], out int id))
                    {
                        resList[id].Add(i);
                    }
                }
            }

            _movieInfoTableProperty.arraySize = resList.Length;
            for (int i = 0; i < resList.Length; i++)
            {
                _movieInfoTableProperty.GetArrayElementAtIndex(i).stringValue = string.Join(",", resList[i].Distinct().OrderBy(x => x));
            }

            _movieInfoProperty.arraySize = titles.Count;
            for (int i = 0; i < titles.Count; i++)
            {
                _movieInfoProperty.GetArrayElementAtIndex(i).stringValue = titles[i];
            }

            _movieInfoString = string.Empty;
        }
    }
}
