﻿using KdTree;
using KdTree.Math;
using PEPExtensions;
using PEPlugin;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using System;
using System.Linq;

namespace UVMirroring
{
    public class UVMirroring : PEPluginClass
    {
        public UVMirroring() : base()
        {
        }

        public override string Name
        {
            get
            {
                return "選択頂点のUVを選択材質の鏡像頂点から転写";
            }
        }

        public override string Version
        {
            get
            {
                return "1.0";
            }
        }

        public override string Description
        {
            get
            {
                return "選択頂点のUVを選択材質の鏡像頂点から転写する";
            }
        }

        public override IPEPluginOption Option
        {
            get
            {
                // boot時実行, プラグインメニューへの登録, メニュー登録名
                return new PEPluginOption(false, true, "選択頂点のUVを選択材質の鏡像頂点から転写");
            }
        }

        public override void Run(IPERunArgs args)
        {
            try
            {
                var pmx = args.Host.Connector.Pmx.GetCurrentState();
                var selectedVertex = args.Host.Connector.View.PmxView.GetSelectedVertexIndices().Select(i => pmx.Vertex[i]).ToList();
                var selecteMaterialIndices = args.Host.Connector.Form.GetSelectedMaterialIndices();
                
                if (!selectedVertex.Any())
                    throw new InvalidOperationException("UVを転写させたい頂点を選択してください。");
                if (!selecteMaterialIndices.Any())
                    throw new InvalidOperationException("UV転写元頂点を含む面が属する材質を選択してください。");

                // 余分な頂点をtreeに入れると重くなるので選択頂点の範囲内(の反転した領域)のみを取り込む
                (V3 min, V3 max) bound = Utility.GetBoundingBox(selectedVertex);
                // 取り込み領域を拡大
                bound.min.Times(1.1);
                bound.max.Times(1.1);
                var tree = new KdTree<float, IPXVertex>(3, new FloatMath(), AddDuplicateBehavior.List);
                var vertices = selecteMaterialIndices.SelectMany(i => Utility.GetMaterialVertices(pmx.Material[i]));
                foreach (var v in vertices
                    .Where(v => v.Position.X.IsWithin(-1 * bound.max.X, -1 * bound.min.X))
                    .Where(v => v.Position.Y.IsWithin(bound.min.Y, bound.max.Y))
                    .Where(v => v.Position.Z.IsWithin(bound.min.Z, bound.max.Z))
                    )
                {
                    tree.Add(v.Position.ToArray(), v);
                }

                foreach (var v in selectedVertex)
                {
                    var vMirror = new V3(v.Position);
                    vMirror.X *= -1;
                    var neighbor = tree.GetNearestNeighbours(vMirror.ToArray(), 1);
                    v.UV = neighbor[0].Duplicate.Aggregate(new V2(0, 0), (sum, vtx) => sum + vtx.UV) / neighbor[0].Duplicate.Count;
                }

                Utility.Update(args.Host.Connector, pmx, PmxUpdateObject.Vertex);
            }
            catch(InvalidOperationException ex)
            {
                Utility.ShowExceptionMessage(ex);
            }
            catch (Exception ex)
            {
                Utility.ShowException(ex);
            }
        }
    }
}
