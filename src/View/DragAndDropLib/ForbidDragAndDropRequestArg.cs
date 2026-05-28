using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DragAndDropLib
{
    /// <summary>
    /// 「カード内容の編集後、カード順が勝手に変更される」に対応するため、DragAndDrop制御用イベントを追加した。
    /// 障害原因：
    ///   DragAndDropLib.ItemsControlDragDropDecoratorはMouseDown, MouseMove, MouseUpにてDragAndDropを制御する。
    ///   ところが、mouse double click時、MouseDownが先勝つが、EditCardDialogを素早く表示すると、
    ///   MouseUpが処理できず、EditCardDialogを閉じるためのクリックのMouseUpに反応され、Cardが移動されたと誤認識される。
    /// </summary>

    public class DisallowDragAndDropRequestArg : EventArgs
    {
    }

    public class AllowDragAndDropRequestArg : EventArgs
    {
    }
}
