"""Run with blender --background --factory-startup --python tests/regression.py."""
import os
import sys
import json
import tempfile
import unittest

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import run as pipeline


class PipelineTests(unittest.TestCase):
    def setUp(self):
        pipeline.reset_scene()
        self.schema, self.bones = pipeline.load_schema(pipeline.default_schema_path())
        self.arm = pipeline.build_armature(self.bones, {"scale": 1, "offset": Vector()}, False)

    def mesh(self, name="Body", vertices=3):
        data = bpy.data.meshes.new(name)
        data.from_pydata([(i % 2, i // 2, 0) for i in range(vertices)], [], [(0, 1, 2)])
        mesh = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(mesh)
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = self.arm
        return mesh

    def mesh_at(self, co, name="Probe"):
        p = Vector(co)
        data = bpy.data.meshes.new(name)
        data.from_pydata([p, p + Vector((0.002, 0, 0)), p + Vector((0, 0.002, 0))], [], [(0, 1, 2)])
        mesh = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(mesh)
        mod = mesh.modifiers.new("Armature", "ARMATURE")
        mod.object = self.arm
        return mesh

    def weight(self, mesh, group, index=0):
        vg = mesh.vertex_groups.get(group)
        if vg is None:
            return 0.0
        gi = vg.index
        if not any(g.group == gi for g in mesh.data.vertices[index].groups):
            return 0.0
        return vg.weight(index)

    def test_industry_object_names(self):
        mesh = self.mesh("Cube")
        pipeline.name_geo_meshes([mesh], "elf")
        self.assertEqual(mesh.name, "GEO-elf")
        self.assertEqual(mesh.data.name, "GEO-elf")
        arm = pipeline.build_armature(self.bones, {"scale": 1, "offset": Vector()}, False, name="RIG-elf")
        self.assertEqual(arm.name, "RIG-elf")
        self.assertEqual(arm.data.name, "RIG-elf")
        self.assertIn("Hips", arm.data.bones)
        self.assertIn("Clavicle.L", arm.data.bones)
        self.assertNotIn("mixamorig:Hips", arm.data.bones)
        self.assertNotIn("LeftUpperArm", arm.data.bones)

    def test_rest_orientation(self):
        for bone in self.arm.data.bones:
            rest = self.bones[bone.name]["rest_armature"]
            for row in range(3):
                for col in range(3):
                    self.assertAlmostEqual(bone.matrix_local[row][col], rest[row][col], places=5, msg=bone.name)

    def test_utility_groups_do_not_count_as_skin(self):
        mesh = self.mesh()
        mesh.vertex_groups.new(name="Mask").add([0, 1, 2], 1, "REPLACE")
        report = pipeline.validate([mesh], self.arm, self.bones)
        self.assertFalse(report["ok"])
        self.assertEqual(report["weights"]["unweighted"], 3)

    def test_all_meshes_and_influence_budget(self):
        bad, good = self.mesh("Bad"), self.mesh("Good")
        good.vertex_groups.new(name="Hips").add([0, 1, 2], 1, "REPLACE")
        for name in ["Hips", "Spine", "Chest", "Neck", "Head"]:
            bad.vertex_groups.new(name=name).add([0, 1, 2], .1, "REPLACE")
        report = pipeline.validate([bad, good], self.arm, self.bones)
        self.assertFalse(report["ok"])
        self.assertEqual(report["weights"]["vertices"], 6)
        self.assertEqual(report["weights"]["over_four_influences"], 3)
        self.assertEqual(report["weights"]["unnormalized"], 3)
        pipeline.prune_influences(bad, 4, self.arm)
        self.assertTrue(pipeline.validate([bad, good], self.arm, self.bones)["ok"])

    def test_modifier_required(self):
        mesh = self.mesh()
        mesh.vertex_groups.new(name="Hips").add([0, 1, 2], 1, "REPLACE")
        mesh.modifiers.clear()
        self.assertFalse(pipeline.validate([mesh], self.arm, self.bones)["ok"])

    def test_refine_drops_opposite_thigh(self):
        bone = self.arm.data.bones["Thigh.L"]
        p = self.arm.matrix_world @ ((bone.head_local + bone.tail_local) * 0.5)
        mesh = self.mesh_at(p)
        mesh.vertex_groups.new(name="Thigh.L").add([0, 1, 2], 0.5, "REPLACE")
        mesh.vertex_groups.new(name="Thigh.R").add([0, 1, 2], 0.5, "REPLACE")
        pipeline.refine_weights(mesh, self.arm)
        self.assertGreater(self.weight(mesh, "Thigh.L"), 0.7)
        self.assertLess(self.weight(mesh, "Thigh.R"), 0.05)

    def test_refine_drops_cross_finger(self):
        bone = self.arm.data.bones["Index2.L"]
        p = self.arm.matrix_world @ ((bone.head_local + bone.tail_local) * 0.5)
        mesh = self.mesh_at(p)
        mesh.vertex_groups.new(name="Index2.L").add([0, 1, 2], 0.5, "REPLACE")
        mesh.vertex_groups.new(name="Middle2.L").add([0, 1, 2], 0.5, "REPLACE")
        pipeline.refine_weights(mesh, self.arm)
        self.assertGreater(self.weight(mesh, "Index2.L"), 0.4)
        self.assertLess(self.weight(mesh, "Middle2.L"), 0.05)

    def test_refine_hips_keeps_both_thighs(self):
        bone = self.arm.data.bones["Hips"]
        p = self.arm.matrix_world @ ((bone.head_local + bone.tail_local) * 0.5)
        mesh = self.mesh_at(p)
        mesh.vertex_groups.new(name="Hips").add([0, 1, 2], 0.6, "REPLACE")
        mesh.vertex_groups.new(name="Thigh.L").add([0, 1, 2], 0.2, "REPLACE")
        mesh.vertex_groups.new(name="Thigh.R").add([0, 1, 2], 0.2, "REPLACE")
        pipeline.refine_weights(mesh, self.arm)
        self.assertGreater(self.weight(mesh, "Hips"), 0.4)
        self.assertGreater(self.weight(mesh, "Thigh.L"), 0.05)
        self.assertGreater(self.weight(mesh, "Thigh.R"), 0.05)

    def test_proxy_transfer_direction(self):
        source, destination = self.mesh("Proxy"), self.mesh("Destination")
        source.vertex_groups.new(name="Hips").add([0, 1, 2], 1, "REPLACE")
        pipeline.transfer_weights(source, destination, self.arm)
        self.assertEqual(pipeline.validate_weights([destination], self.arm)["unweighted"], 0)

    def test_repair_refreshes_canonical_reports(self):
        mesh = self.mesh()
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "test_blender_result.json")
            pipeline.write_result(path, {"ok": True, "textures": ["albedo.png"]})
            report = pipeline.validate([mesh], self.arm, self.bones)
            pipeline.write_repair_result(folder, "test", report, "test.fbx", "test.blend")
            with open(path) as stream:
                result = json.load(stream)
            self.assertFalse(result["ok"])
            self.assertEqual(result["textures"], ["albedo.png"])
            with open(result["validation"]) as stream:
                self.assertEqual(json.load(stream)["weights"]["unweighted"], 3)


if __name__ == "__main__":
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(PipelineTests))
    sys.exit(0 if result.wasSuccessful() else 1)
