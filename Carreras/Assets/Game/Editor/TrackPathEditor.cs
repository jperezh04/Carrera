using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StreetRacing.Environment;

namespace StreetRacing.EditorTools
{
    [CustomEditor(typeof(TrackPath))]
    public class TrackPathEditor : Editor
    {
        static bool autoRegenerate = true;
        int selectedIndex = -1;

        public override void OnInspectorGUI()
        {
            var track = (TrackPath)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "EDITOR DE CIRCUITO URBANO\n" +
                "• Selecciona este objeto y mueve los nodos azules directamente en la Scene View.\n" +
                "• Ajusta 'Road Width' para cambiar el ancho de la calzada.\n" +
                "• Pulsa 'Regenerar Todo el Circuito' tras editar o activa la regeneración automática.",
                MessageType.Info);

            autoRegenerate = EditorGUILayout.ToggleLeft("Regenerar carretera automáticamente al mover nodos", autoRegenerate);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool inspectorChanged = EditorGUI.EndChangeCheck();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Acciones de Construcción", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.45f);
            if (GUILayout.Button("Regenerar Carretera, Barreras y Checkpoints", GUILayout.Height(32)))
            {
                Undo.RegisterFullObjectHierarchyUndo(track.gameObject, "Regenerate Track");
                track.RebuildTrack(true);
                var dec = track.GetComponent<TrackEnvironmentDecorator>();
                if (dec) dec.RebuildRoadsideElements();
                MarkDirty(track);
            }
            GUI.backgroundColor = Color.white;

            var decorator = track.GetComponent<TrackEnvironmentDecorator>();
            if (decorator)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Actualizar Farolas y Meta"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(decorator.gameObject, "Rebuild Roadside");
                    decorator.RebuildRoadsideElements();
                    MarkDirty(track);
                }
                if (GUILayout.Button("Reubicar Edificios Exteriores"))
                {
                    Undo.RegisterFullObjectHierarchyUndo(decorator.gameObject, "Rebuild Buildings");
                    decorator.RebuildCityBuildings();
                    MarkDirty(track);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Edición de Puntos de Control", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Insertar Punto"))
            {
                Undo.RecordObject(track, "Insert Track Point");
                int n = track.controlPoints.Count;
                int idx = (selectedIndex >= 0 && selectedIndex < n) ? selectedIndex : n - 1;
                int next = (idx + 1) % n;
                Vector3 mid = (track.controlPoints[idx] + track.controlPoints[next]) * 0.5f;
                track.controlPoints.Insert(idx + 1, mid);
                selectedIndex = idx + 1;
                track.RebuildTrack(true);
                MarkDirty(track);
            }
            if (GUILayout.Button("- Eliminar Punto") && track.controlPoints.Count > 4)
            {
                Undo.RecordObject(track, "Remove Track Point");
                int idx = (selectedIndex >= 0 && selectedIndex < track.controlPoints.Count)
                    ? selectedIndex : track.controlPoints.Count - 1;
                track.controlPoints.RemoveAt(idx);
                selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, track.controlPoints.Count - 1);
                track.RebuildTrack(true);
                MarkDirty(track);
            }
            if (GUILayout.Button("Nivelar Altura (Y=0)"))
            {
                Undo.RecordObject(track, "Flatten Track Heights");
                for (int i = 0; i < track.controlPoints.Count; i++)
                {
                    Vector3 p = track.controlPoints[i];
                    p.y = 0f;
                    track.controlPoints[i] = p;
                }
                track.RebuildTrack(true);
                MarkDirty(track);
            }
            EditorGUILayout.EndHorizontal();

            if (inspectorChanged && autoRegenerate)
            {
                track.RebuildTrack(false);
                SceneView.RepaintAll();
            }
        }

        void OnSceneGUI()
        {
            var track = (TrackPath)target;
            if (track.controlPoints == null || track.controlPoints.Count < 3) return;

            track.SampleSpline();

            // Draw road centerline & width edges in Scene View
            Handles.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            int sc = track.sampledPoints.Count;
            float halfW = track.roadWidth * 0.5f;
            for (int i = 0; i < sc; i++)
            {
                int next = (i + 1) % sc;
                Handles.DrawLine(track.sampledPoints[i] + Vector3.up * 0.15f, track.sampledPoints[next] + Vector3.up * 0.15f);
            }

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.45f);
            for (int i = 0; i < sc; i += 2)
            {
                int next = (i + 2) % sc;
                Vector3 l0 = track.sampledPoints[i] - track.sampledRights[i] * halfW + Vector3.up * 0.1f;
                Vector3 l1 = track.sampledPoints[next] - track.sampledRights[next] * halfW + Vector3.up * 0.1f;
                Vector3 r0 = track.sampledPoints[i] + track.sampledRights[i] * halfW + Vector3.up * 0.1f;
                Vector3 r1 = track.sampledPoints[next] + track.sampledRights[next] * halfW + Vector3.up * 0.1f;
                Handles.DrawLine(l0, l1);
                Handles.DrawLine(r0, r1);
            }

            // Interactive Handles for every control point
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.yellow },
                fontSize = 12
            };

            for (int i = 0; i < track.controlPoints.Count; i++)
            {
                Vector3 worldPos = track.transform.TransformPoint(track.controlPoints[i]);
                string label = i == 0 ? $"#{i} [SALIDA / META] (Y:{track.controlPoints[i].y:F1}m)" : $"#{i} (Y:{track.controlPoints[i].y:F1}m)";
                Handles.Label(worldPos + Vector3.up * 1.8f, label, labelStyle);

                Handles.color = (i == selectedIndex) ? Color.yellow : (i == 0 ? Color.green : Color.cyan);
                float size = HandleUtility.GetHandleSize(worldPos) * 0.15f;
                if (Handles.Button(worldPos + Vector3.up * 1.1f, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    selectedIndex = i;
                    Repaint();
                }

                EditorGUI.BeginChangeCheck();
                Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    selectedIndex = i;
                    Undo.RecordObject(track, "Move Track Control Point");
                    track.controlPoints[i] = track.transform.InverseTransformPoint(newWorldPos);
                    if (autoRegenerate)
                        track.RebuildTrack(false);
                    else
                        track.SampleSpline();
                    MarkDirty(track);
                }
            }
        }

        static void MarkDirty(TrackPath track)
        {
            EditorUtility.SetDirty(track);
            if (!Application.isPlaying && track.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
        }
    }
}
