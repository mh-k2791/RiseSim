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
using RiseSim.ViewModels;
using SimModel.model;
using SimModel.service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RiseSim.Views
{
    /// <summary>
    /// MainView.xaml の相互作用ロジック
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();

        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).Search();
        }

        private void SearchMoreButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).SearchMore();
        }

        private void SearchExtraSkillButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).SearchExtraSkill();
        }

        private void ResultGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            EquipSet? set = resultGrid.CurrentItem as EquipSet;
            if (set != null)
            {
                ((MainViewModel)DataContext).ViewSetDetail(set);
            }
        }

        private void MySetGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            EquipSet? set = mySetGrid.CurrentItem as EquipSet;
            if (set != null)
            {
                ((MainViewModel)DataContext).ViewMySetDetail(set);
            }
        }

        private void CharmAddButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).AddCharm();
        }

        private void MySetAddButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).AddMySet();
        }

        private void MySetDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).DeleteMySet();
        }

        private void MySetToConditionButton_Click(object sender, RoutedEventArgs e)
        {
            ((MainViewModel)DataContext).InputMySetCondition();
        }
    }
}