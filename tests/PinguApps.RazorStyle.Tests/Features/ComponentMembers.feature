Feature: Component member conventions
    Authored partial components have predictable member layout without changing behaviour.

Scenario: Compiler errors withhold a fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            private void Work() { Missing(); }
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Fixes preserve attributes and cleanup relative order
    Given the component C# source is
        """
        public partial class Example : ComponentBase, IDisposable, IAsyncDisposable
        {
            public ValueTask DisposeAsync() => default;
            public void Dispose() { }
            // Keep this parameter comment.
            [ParameterAttribute, EditorRequired] public int Value { get; set; }
            private void Work() { }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics
    And there are 0 safe component ordering fixes
    And the component source contains "[ParameterAttribute, EditorRequired] public int Value { get; set; }"
    And the component source contains "Keep this parameter comment."
    And component member "Work()" appears before "DisposeAsync()"
    And component member "DisposeAsync()" appears before "void Dispose()"

Scenario: Explicit interface properties and events retain their groups
    Given the component C# source is
        """
        public interface IContract { int State { get; } event Action Updated; }
        public partial class Example : ComponentBase, IContract
        {
            event Action IContract.Updated { add { } remove { } }
            int IContract.State => 0;
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics
    And component member "IContract.State" appears before "IContract.Updated"

Scenario: Generic content follows ordinary optional parameters
    Given the component C# source is
        """
        public partial class Example<T> : ComponentBase
        {
            [Parameter] public RenderFragment<T> Template { get; set; }
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Binding partners in different files have both locations
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            [Parameter] public int Value { get; set; }
        }
        """
    And another authored C# part is
        """
        public partial class Example
        {
            [Parameter] public EventCallback<int> ValueChanged { get; set; }
        }
        """
    When component conventions are checked
    Then there are 2 component ordering diagnostics
    And each ordering diagnostic identifies its binding partner
    And there are 0 safe component ordering fixes

Scenario: Aliased framework parameters retain semantic identity
    Given the component C# source is
        """
        using Param = Microsoft.AspNetCore.Components.ParameterAttribute;
        public partial class Example : ComponentBase
        {
            private int state;
            [Param] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Every lifecycle hook follows the approved sequence
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
            protected override void OnAfterRender(bool firstRender) { }
            protected override bool ShouldRender() => true;
            protected override Task OnParametersSetAsync() => Task.CompletedTask;
            protected override void OnParametersSet() { }
            protected override Task OnInitializedAsync() => Task.CompletedTask;
            protected override void OnInitialized() { }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Conditional member boundaries prevent a fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
        #if true
            private void Work() { }
        #endif
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Fix All orders multiple components without generating members
    Given the component C# source is
        """
        public partial class First : ComponentBase
        {
            protected override Task OnParametersSetAsync() => Task.CompletedTask;
            protected override void OnInitialized() { }
        }
        public partial class Second : ComponentBase
        {
            private void Work() { }
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 2 component ordering diagnostics
    When component ordering Fix All is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Unmatched Changed names are standalone callbacks
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            [Parameter] public int Value { get; set; }
            [Parameter] public EventCallback<string> ValueChanged { get; set; }
            [Parameter] public EventCallback MissingChanged { get; set; }
            [Parameter] public RenderFragment Content { get; set; }
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: User-defined lookalike attributes are not framework parameters
    Given the component C# source is
        """
        public class ParameterAttribute : Attribute { }
        public partial class Example : ComponentBase
        {
            private int state;
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Incomplete code does not crash or offer a fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            private void Work() { }
            [Parameter] public int Value { get; set; }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Untyped callbacks support value binding
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            [Parameter, EditorRequired] public int Value { get; set; }
            [Parameter] public EventCallback ValueChanged { get; set; }
            [Parameter, EditorRequired] public int Other { get; set; }
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario Outline: Member groups follow the convention
    Given the component C# source is
        """
        public partial class Example : ComponentBase, IDisposable, IAsyncDisposable
        {
            <later>
            <earlier>
            public void Dispose() { }
            public ValueTask DisposeAsync() => default;
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 1 safe component ordering fixes
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics
    Examples:
        | earlier | later |
        | [CascadingParameter] public string Context { get; set; } | [Parameter, EditorRequired] public int Value { get; set; } |
        | [Parameter, EditorRequired] public int Value { get; set; } | [Parameter] public int Optional { get; set; } |
        | [Parameter] public int Value { get; set; } | [Parameter] public EventCallback OnSaved { get; set; } |
        | [Parameter] public EventCallback OnSaved { get; set; } | [Parameter] public RenderFragment Content { get; set; } |
        | [Parameter] public RenderFragment Content { get; set; } | private int state; |
        | private int state; | public Example() { } |
        | public Example() { } | public int State { get; set; } |
        | public int State { get; set; } | public event Action Saved; |
        | public event Action Saved; | protected override void OnInitialized() { } |
        | protected override void OnInitialized() { } | private void Work() { } |

Scenario: Generic nullable binding pairs are moved as units
    Given the component C# source is
        """
        public partial class Example<T> : ComponentBase where T : struct
        {
            [Parameter] public bool Disabled { get; set; }
            [Parameter] public EventCallback<T?> ValueChanged { get; set; }
            [Parameter, EditorRequired] public T? Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 1 safe component ordering fixes
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Explicit cleanup methods belong below helpers
    Given the component C# source is
        """
        public partial class Example : ComponentBase, IDisposable, IAsyncDisposable
        {
            void IDisposable.Dispose() { }
            ValueTask IAsyncDisposable.DisposeAsync() => default;
            private void Work() { }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Framework aliases and inherited lifecycle overrides work
    Given the component C# source is
        """
        public class Base : ComponentBase { protected override void OnInitialized() { } }
        public partial class Example : Base
        {
            protected override Task OnInitializedAsync() => Task.CompletedTask;
            protected override void OnInitialized() { }
            [global::Microsoft.AspNetCore.Components.ParameterAttribute]
            public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Ordinary same-named methods and non-parameter callbacks retain their order
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            public int State;
            public EventCallback<int> ValueChanged { get; set; }
            public int Value { get; set; }
            public void OnAfterRender(string value) { }
            public void Dispose() { }
            public void OnInitialized(int value) { }
            public override Task SetParametersAsync(ParameterView parameters) => base.SetParametersAsync(parameters);
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Cross-part binding pairs have no automatic fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            [Parameter] public int Value { get; set; }
        }
        public partial class Example
        {
            [Parameter] public EventCallback<int> ValueChanged { get; set; }
        }
        """
    When component conventions are checked
    Then there are 2 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Regions prevent an automatic fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
        #region Helpers
            private void Work() { }
        #endregion
            [Parameter] public int Value { get; set; }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Generated declarations are ignored
    Given the component C# source is
        """
        [System.CodeDom.Compiler.GeneratedCode("Razor", "1")]
        public partial class Example : ComponentBase
        {
            private void Work() { }
            public int State;
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Required binding groups remain together
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            [Parameter, EditorRequired] public string Title { get; set; } = "";
            [Parameter] public EventCallback<string> TitleChanged { get; set; }
            [Parameter, EditorRequired] public int Value { get; set; }
            [Parameter] public EventCallback<int> ValueChanged { get; set; }
            [Parameter] public bool Disabled { get; set; }
            [Parameter] public EventCallback<bool> DisabledChanged { get; set; }
            [Parameter] public EventCallback OnSaved { get; set; }
            [Parameter] public RenderFragment ChildContent { get; set; }
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics

Scenario: Lifecycle order can be fixed idempotently
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            /// <summary>Keep these docs.</summary>
            protected override Task OnInitializedAsync() => Task.CompletedTask;
            protected override void OnInitialized() { }
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 1 safe component ordering fixes
    When the safe component ordering fix is applied
    And component conventions are checked
    Then there are 0 component ordering diagnostics
    And the component source contains "Keep these docs."

Scenario: Observable initialization order prevents a fix
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            private int state = Next();
            [Parameter] public int Value { get; set; } = Next();
            private static int counter;
            private static int Next() => ++counter;
        }
        """
    When component conventions are checked
    Then there are 1 component ordering diagnostics
    And there are 0 safe component ordering fixes

Scenario: Nested types require manual extraction
    Given the component C# source is
        """
        public partial class Example : ComponentBase
        {
            private class Item { }
            private struct State { }
            private record Data;
            private enum Mode { Idle }
            private interface IHelper { }
            private delegate void Handler();
        }
        """
    When component conventions are checked
    Then there are 6 nested component type diagnostics
    And there are 0 safe component ordering fixes

Scenario: Ordinary classes are unaffected
    Given the component C# source is
        """
        public partial class Example
        {
            public void Work() { }
            public int State;
            private class Item { }
        }
        """
    When component conventions are checked
    Then there are 0 component ordering diagnostics
    And there are 0 nested component type diagnostics
