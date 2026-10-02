using Majorsilence.Forms.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using Majorsilence.Forms;
using Xunit;

namespace AlertBuddy.Shared.Tests
{
    public partial class FormBindingsTests
    {
        private sealed partial class Model : ObservableObject
        {
            [ObservableProperty] private string name = "";
            [ObservableProperty] private bool on;
            [ObservableProperty] private int count;
            public int NameWrites;
            public bool Shout;

            partial void OnNameChanged (string value)
            {
                NameWrites++;
                if (Shout && value != value.ToUpperInvariant ())
                    Name = value.ToUpperInvariant ();
            }
        }

        [Fact]
        public void BindText_FollowsTheViewModel_AndWritesBack_WithoutLooping ()
        {
            var vm = new Model { Name = "Pip" };
            var box = new TextBox ();
            using var binding = box.BindText (vm, nameof (Model.Name), v => v.Name, (v, t) => v.Name = t);

            Assert.Equal ("Pip", box.Text);

            vm.Name = "Sunny";
            Assert.Equal ("Sunny", box.Text);

            var writesBefore = vm.NameWrites;
            box.Text = "Bo";
            Assert.Equal ("Bo", vm.Name);
            Assert.Equal (writesBefore + 1, vm.NameWrites);
        }

        [Fact]
        public void BindText_DoesNotWriteBackIntoTheBoxWhileItIsBeingTypedIn ()
        {
            // A view model that rewrites what it is given must not make the box rewrite the person's typing under their caret: the
            // change event the rewrite would raise has to be swallowed by the guard, not answered.
            var vm = new Model { Shout = true };
            var box = new TextBox ();
            using var binding = box.BindText (vm, nameof (Model.Name), v => v.Name, (v, t) => v.Name = t);
            var changes = 0;
            box.TextChanged += (_, _) => changes++;

            box.Text = "ab";

            Assert.Equal ("AB", vm.Name);
            Assert.Equal ("ab", box.Text);
            Assert.Equal (1, changes);
        }

        [Fact]
        public void BindText_StopsWhenDisposed ()
        {
            var vm = new Model ();
            var box = new TextBox ();
            var binding = box.BindText (vm, nameof (Model.Name), v => v.Name, (v, t) => v.Name = t);
            binding.Dispose ();

            vm.Name = "after";
            box.Text = "typed";

            Assert.Equal ("typed", box.Text);
            Assert.Equal ("after", vm.Name);
        }

        [Fact]
        public void BindChecked_AndBindValue_GoBothWays ()
        {
            var vm = new Model ();
            var check = new CheckBox ();
            var number = new NumericUpDown { Minimum = 1, Maximum = 240 };
            using var a = check.BindChecked (vm, nameof (Model.On), v => v.On, (v, c) => v.On = c);
            using var b = number.BindValue (vm, nameof (Model.Count), v => v.Count, (v, n) => v.Count = (int)n);

            check.Checked = true;
            Assert.True (vm.On);
            vm.On = false;
            Assert.False (check.Checked);

            number.Value = 30;
            Assert.Equal (30, vm.Count);
            vm.Count = 99;
            Assert.Equal (99, (int)number.Value);
        }
    }
}
