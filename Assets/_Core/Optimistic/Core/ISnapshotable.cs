namespace GRoll.Core.Optimistic
{
    /// <summary>
    /// State'i snapshot alıp restore edebilen interface.
    /// Optimistic operation'larda rollback için kullanılır.
    ///
    /// Kullanım:
    /// <code>
    /// public class InventoryState : ISnapshotable&lt;InventorySnapshot&gt;
    /// {
    ///     public InventorySnapshot CreateSnapshot()
    ///     {
    ///         return new InventorySnapshot
    ///         {
    ///             EquippedItems = new Dictionary&lt;string, string&gt;(_equippedItems),
    ///             OwnedItems = new List&lt;string&gt;(_ownedItems)
    ///         };
    ///     }
    ///
    ///     public void RestoreSnapshot(InventorySnapshot snapshot)
    ///     {
    ///         _equippedItems = new Dictionary&lt;string, string&gt;(snapshot.EquippedItems);
    ///         _ownedItems = new List&lt;string&gt;(snapshot.OwnedItems);
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="TSnapshot">Snapshot veri tipi</typeparam>
    public interface ISnapshotable<TSnapshot>
    {
        /// <summary>
        /// Mevcut state'in immutable kopyasını oluşturur.
        /// Bu kopya daha sonra restore için kullanılacaktır.
        /// </summary>
        /// <returns>State'in kopyası</returns>
        TSnapshot CreateSnapshot();

        /// <summary>
        /// Snapshot'tan state'i restore eder (rollback).
        /// Bu metod çağrıldığında state, snapshot alındığı andaki durumuna döner.
        /// </summary>
        /// <param name="snapshot">Restore edilecek snapshot</param>
        void RestoreSnapshot(TSnapshot snapshot);
    }
}
