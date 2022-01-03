﻿/*    RiseSim : MHRise skill simurator for Windows
 *    Copyright (C) 2022  EXXXI
 *
 *    This program is free software: you can redistribute it and/or modify
 *    it under the terms of the GNU General Public License as published by
 *    the Free Software Foundation, either version 3 of the License, or
 *    (at your option) any later version.
 *
 *    This program is distributed in the hope that it will be useful,
 *    but WITHOUT ANY WARRANTY; without even the implied warranty of
 *    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *    GNU General Public License for more details.
 *
 *    You should have received a copy of the GNU General Public License
 *    along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using GlpkWrapperCS;
using SimModel.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimModel.domain
{
    internal class Searcher
    {
        // 定数：各制約式のIndex
        const int HeadRowIndex = 0;
        const int BodyRowIndex = 1;
        const int ArmRowIndex = 2;
        const int WaistRowIndex = 3;
        const int LegRowIndex = 4;
        const int CharmRowIndex = 5;
        const int Slot1RowIndex = 6;
        const int Slot2RowIndex = 7;
        const int Slot3RowIndex = 8;
        const int Slot4RowIndex = 9;

        // 検索条件
        public SearchCondition Condition { get; set; }

        // 検索結果
        public List<EquipSet> ResultSets { get; set; }

        // スキル条件の制約式の開始Index
        private int FirstSkillRowIndex { get; set; }

        // 検索結果除外条件の制約式の開始Index
        private int FirstResultExcludeRowIndex { get; set; }

        // 除外・固定条件の制約式の開始Index
        private int FirstCludeRowIndex { get; set; }

        // コンストラクタ：検索条件を指定する
        public Searcher(SearchCondition condition)
        {
            Condition = condition;
            ResultSets = new List<EquipSet>();
        }

        // 検索
        public void ExecSearch(int limit)
        {
            // 目標検索件数
            int target = ResultSets.Count + limit;


            while (ResultSets.Count < target)
            {
                using (MipProblem problem = new MipProblem())
                {
                    problem.Name = "search";
                    problem.ObjDir = ObjectDirection.Maximize;

                    // 制約式設定
                    SetRows(problem);

                    // 変数設定
                    SetColumns(problem);

                    // 目的関数設定(防御力)
                    SetCoef(problem);

                    // 係数設定(防具データ)
                    SetDatas(problem);

                    // 計算
                    var result = problem.BranchAndCut();
                    if (!result.Equals(SolverResult.OK))
                    {
                        // もう結果がヒットしない場合終了
                        break;
                    }

                    // 計算結果整理
                    bool hasData = MakeSet(problem);
                    if (!hasData)
                    {
                        // TODO: 何故発生する？
                        // 空データが出現したら終了
                        break;
                    }
                }
            }
        }

        // 制約式設定
        private void SetRows(MipProblem problem)
        {
            // 各部位に装着できる防具は1つまで
            problem.AddRow("head");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);
            problem.AddRow("body");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);
            problem.AddRow("arm");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);
            problem.AddRow("waist");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);
            problem.AddRow("leg");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);
            problem.AddRow("charm");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Double, 0.0, 1.0);

            // 武器スロ計算
            int[] slotCond = SlotCalc(Condition.WeaponSlot1, Condition.WeaponSlot2, Condition.WeaponSlot3);

            // 残りスロット数は0以上
            problem.AddRow("Slot1");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Lower, 0.0 - slotCond[0], 0.0);
            problem.AddRow("Slot2");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Lower, 0.0 - slotCond[1], 0.0);
            problem.AddRow("Slot3");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Lower, 0.0 - slotCond[2], 0.0);
            problem.AddRow("Slot4");
            problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Lower, 0.0 - slotCond[3], 0.0);

            // スキル条件
            FirstSkillRowIndex = problem.RowsCount;
            foreach (var skill in Condition.Skills)
            {
                problem.AddRow(skill.Name);
                problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Lower, skill.Level, 0.0);
            }

            // 検索済み結果の除外
            FirstResultExcludeRowIndex = problem.RowsCount;
            foreach (var set in ResultSets)
            {
                problem.AddRow(set.SimpleSetNameWithoutDecos);
                problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Upper, 0.0, set.EquipIndexsWithOutDecos.Count - 1);
            }

            // 除外固定装備設定
            FirstCludeRowIndex = problem.RowsCount;
            foreach (var clude in Masters.Cludes)
            {
                string nameSuffix = "_ex";
                int fix = 0;
                if (clude.Kind.Equals(CludeKind.include))
                {
                    nameSuffix = "_in";
                    fix = 1;
                }
                problem.AddRow(clude.Name + nameSuffix);
                problem.SetRowBounds(problem.RowsCount - 1, BoundsType.Fixed, fix, fix);
            }

        }

        // 変数設定
        private static void SetColumns(MipProblem problem)
        {
            // 各装備は0個以上で整数
            foreach (var equip in Masters.Heads)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Bodys)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Arms)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Waists)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Legs)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Charms)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
            foreach (var equip in Masters.Decos)
            {
                problem.AddColumn(equip.Name);
                problem.SetColumnBounds(problem.ColumnsCount - 1, BoundsType.Lower, 0.0, 0.0);
                problem.ColumnKind[problem.ColumnsCount - 1] = VariableKind.Integer;
            }
        }

        // TODO: 防御力以外も対応する？
        // 目的関数設定(防御力)
        private static void SetCoef(MipProblem problem)
        {
            // 各装備の防御力が、目的関数における各装備の項の係数となる
            int columnIndex = 0;
            foreach (var equip in Masters.Heads)
            {
                problem.ObjCoef[columnIndex++] = equip.Maxdef;
            }
            foreach (var equip in Masters.Bodys)
            {
                problem.ObjCoef[columnIndex++] = equip.Maxdef;
            }
            foreach (var equip in Masters.Arms)
            {
                problem.ObjCoef[columnIndex++] = equip.Maxdef;
            }
            foreach (var equip in Masters.Waists)
            {
                problem.ObjCoef[columnIndex++] = equip.Maxdef;
            }
            foreach (var equip in Masters.Legs)
            {
                problem.ObjCoef[columnIndex++] = equip.Maxdef;
            }
        }

        // 係数設定(防具データ)
        private void SetDatas(MipProblem problem)
        {
            List<int> iaList = new List<int>();
            List<int> jaList = new List<int>();
            List<double> arList = new List<double>();

            // 防具データ
            int columnIndex = 0;
            foreach (var equip in Masters.Heads)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Bodys)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Arms)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Waists)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Legs)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Charms)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }
            foreach (var equip in Masters.Decos)
            {
                SetEquipData(iaList, jaList, arList, columnIndex, equip);
                columnIndex++;
            }

            // 検索済みデータ
            int resultExcludeRowIndex = FirstResultExcludeRowIndex;
            foreach (var set in ResultSets)
            {
                List<int> indexList = set.EquipIndexsWithOutDecos;
                foreach (var index in indexList)
                {
                    // 各装備に対応する係数を1とする
                    iaList.Add(resultExcludeRowIndex);
                    jaList.Add(index);
                    arList.Add(1);
                }
                resultExcludeRowIndex++;
            }

            // 除外固定データ
            int cludeRowIndex = FirstCludeRowIndex;
            foreach (var clude in Masters.Cludes)
            {
                // 装備に対応する係数を1とする
                int index = Masters.GetEquipIndexByName(clude.Name);
                iaList.Add(cludeRowIndex);
                jaList.Add(index);
                arList.Add(1);
                cludeRowIndex++;
            }

            int[] ia = iaList.ToArray();
            int[] ja = jaList.ToArray();
            double[] ar = arList.ToArray();

            problem.LoadMatrix(ia, ja, ar);
        }

        // 装備のデータを係数として登録
        private void SetEquipData(List<int> iaList, List<int> jaList, List<double> arList, int columnIndex, Equipment equip)
        {
            // 部位情報
            int kindIndex = 0;
            bool isDeco = false;
            switch (equip.Kind)
            {
                case EquipKind.head:
                    kindIndex = HeadRowIndex;
                    break;
                case EquipKind.body:
                    kindIndex = BodyRowIndex;
                    break;
                case EquipKind.arm:
                    kindIndex = ArmRowIndex;
                    break;
                case EquipKind.waist:
                    kindIndex = WaistRowIndex;
                    break;
                case EquipKind.leg:
                    kindIndex = LegRowIndex;
                    break;
                case EquipKind.charm:
                    kindIndex = CharmRowIndex;
                    break;
                default:
                    isDeco = true;
                    break;
            }
            if (!isDeco)
            {
                iaList.Add(kindIndex);
                jaList.Add(columnIndex);
                arList.Add(1);
            }

            // スロット情報
            int[] slotCond = SlotCalc(equip.Slot1, equip.Slot2, equip.Slot3);
            if (isDeco)
            {
                for (int i = 0; i < slotCond.Length; i++)
                {
                    slotCond[i] = slotCond[i] * -1;
                }
            }
            iaList.Add(Slot1RowIndex);
            jaList.Add(columnIndex);
            arList.Add(slotCond[0]);
            iaList.Add(Slot2RowIndex);
            jaList.Add(columnIndex);
            arList.Add(slotCond[1]);
            iaList.Add(Slot3RowIndex);
            jaList.Add(columnIndex);
            arList.Add(slotCond[2]);
            iaList.Add(Slot4RowIndex);
            jaList.Add(columnIndex);
            arList.Add(slotCond[3]);

            // スキル情報
            foreach (var condSkill in Condition.Skills)
            {
                foreach (var equipSkill in equip.Skills)
                {
                    if (equipSkill.Name.Equals(condSkill.Name))
                    {
                        iaList.Add(FirstSkillRowIndex + Condition.Skills.IndexOf(condSkill));
                        jaList.Add(columnIndex);
                        arList.Add(equipSkill.Level);
                    }
                }
            }
        }

        // 計算結果整理
        private bool MakeSet(MipProblem problem)
        {
            EquipSet equipSet = new EquipSet();
            bool hasData = false;
            for (int i = 0; i < problem.ColumnsCount; i++)
            {
                if (problem.MipColumnValue[i] > 0)
                {
                    // 装備名
                    string name = problem.ColumnName[i];

                    // 存在チェック
                    Equipment? equip = Masters.GetEquipByName(name);
                    if (equip == null)
                    {
                        // 存在しない装備名の場合無視
                        // 護石削除関係でバグっていた場合の対策
                        continue;
                    }
                    hasData = true;

                    // 装備種類確認
                    switch (equip.Kind)
                    {
                        case EquipKind.head:
                            equipSet.HeadName = name;
                            break;
                        case EquipKind.body:
                            equipSet.BodyName = name;
                            break;
                        case EquipKind.arm:
                            equipSet.ArmName = name;
                            break;
                        case EquipKind.waist:
                            equipSet.WaistName = name;
                            break;
                        case EquipKind.leg:
                            equipSet.LegName = name;
                            break;
                        case EquipKind.deco:
                            for (int j = 0; j < problem.MipColumnValue[i]; j++)
                            {
                                // 装飾品は個数を確認し、その数追加
                                equipSet.DecoNames.Add(name);
                            }
                            break;
                        case EquipKind.charm:
                            equipSet.CharmName = name;
                            break;
                        default:
                            break;
                    }
                }
            }

            if (hasData)
            {
                // 装備セットにスロット情報を付加
                equipSet.WeaponSlot1 = Condition.WeaponSlot1;
                equipSet.WeaponSlot2 = Condition.WeaponSlot2;
                equipSet.WeaponSlot3 = Condition.WeaponSlot3;

                // 装備セットの各種情報を計算
                equipSet.Calc();

                // 重複する結果(今回の結果に無駄な装備を加えたもの)が既に見つかっていた場合、それを削除
                RemoveDuplicateSet(equipSet);

                // 検索結果に追加
                ResultSets.Add(equipSet);

                // 成功
                return true;
            }

            // 失敗
            return false;
        }

        // 重複する結果(今回の結果に無駄な装備を加えたもの)が既に見つかっていた場合、それを削除
        private void RemoveDuplicateSet(EquipSet newSet)
        {
            List<EquipSet> removeList = new List<EquipSet>();
            foreach (var set in ResultSets)
            {
                if (!IsDuplicateEquipName(newSet.HeadName, set.HeadName))
                {
                    continue;
                }
                if (!IsDuplicateEquipName(newSet.BodyName, set.BodyName))
                {
                    continue;
                }
                if (!IsDuplicateEquipName(newSet.ArmName, set.ArmName))
                {
                    continue;
                }
                if (!IsDuplicateEquipName(newSet.WaistName, set.WaistName))
                {
                    continue;
                }
                if (!IsDuplicateEquipName(newSet.LegName, set.LegName))
                {
                    continue;
                }
                if (!IsDuplicateEquipName(newSet.CharmName, set.CharmName))
                {
                    continue;
                }

                // 全ての部位で重複判定を満たしたため削除
                removeList.Add(set);
            }

            foreach (var set in removeList)
            {
                ResultSets.Remove(set);
            }
        }

        // 重複判定
        private bool IsDuplicateEquipName(string newName, string oldName)
        {
            return string.IsNullOrWhiteSpace(newName) || newName.Equals(oldName);
        }

        // スロットの計算
        // 例：3-1-1→1スロ以下3個2スロ以下3個3スロ以下1個
        private int[] SlotCalc(int slot1, int slot2, int slot3)
        {
            int[] slotCond = new int[4];
            for (int i = 0; i < slot1; i++)
            {
                slotCond[i]++;
            }
            for (int i = 0; i < slot2; i++)
            {
                slotCond[i]++;
            }
            for (int i = 0; i < slot3; i++)
            {
                slotCond[i]++;
            }
            return slotCond;
        }
    }
}