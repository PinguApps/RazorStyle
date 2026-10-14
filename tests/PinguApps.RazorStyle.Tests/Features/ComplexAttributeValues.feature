Feature: Complex Razor attribute values

  @razorstyle
  Scenario: The reported Radzen button preserves its interpolated text and callback
    Given the Razor source is
      """
      <RadzenButton class="rz-mt-3" ButtonType="ButtonType.Submit" Size="ButtonSize.Small"
                                                Text="@($"Send to {_systemUser?.InternalEmailAddress}")"
                                                Click=@(_ => SetAction(SendContent))>
                                  </RadzenButton>
      """
    When the Razor tags are scanned
    Then 1 Razor start tags should be found
    And tag 1 should have these attributes
      | Attribute                                                 |
      | class="rz-mt-3"                                            |
      | ButtonType="ButtonType.Submit"                             |
      | Size="ButtonSize.Small"                                   |
      | Text="@($"Send to {_systemUser?.InternalEmailAddress}")"    |
      | Click=@(_ => SetAction(SendContent))                       |
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      <RadzenButton class="rz-mt-3"
                    ButtonType="ButtonType.Submit"
                    Size="ButtonSize.Small"
                    Text="@($"Send to {_systemUser?.InternalEmailAddress}")"
                    Click=@(_ => SetAction(SendContent))>
                                  </RadzenButton>
      """
    And the rewritten source should pass check and remain unchanged by another fix

  @razorstyle
  Scenario Outline: Formatting preserves C sharp expressions inside attribute values
    Given the Razor source is
      """
      <Widget Text=<value> class="wide" />
      """
    When the Razor tags are scanned
    Then 1 Razor start tags should be found
    And tag 1 should have these attributes
      | Attribute    |
      | Text=<value> |
      | class="wide" |
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      <Widget class="wide"
              Text=<value> />
      """
    And the rewritten source should pass check and remain unchanged by another fix

    Examples:
      | value                                                                  |
      | "@("Send to user")"                                                    |
      | '@($"Send to {user}")'                                                 |
      | @($"Send to {user}")                                                   |
      | "@Format("Send to user")"                                              |
      | "@($"Send to {Format("Last, First")}")"                                 |
      | "@($"Send to {Format(")", "<Fake A B />")}")"                            |
      | "@("Send \"now\" to user")"                                            |
      | "@(@"Send ""now"" to user")"                                           |
      | "@(@"C:\folder\")"                                                     |
      | "@("""Send ")" to user""")"                                            |
      | "@($$"""Send {{Format(")")}} to user""")"                               |
      | "@(Format(')', "Send to user"))"                                        |
      | "@(Format(/* ) \" <Fake A B /> */ "Send to user"))"                       |
      | "Before @("Send to user") after @($"{user}")"                            |
      | "Literal @@( text"                                                     |
      | "Literal ( text with C:\folder\"                                        |

  @razorstyle
  Scenario: Formatting preserves multiline Razor expressions
    Given the Razor source is
      """
      <Widget Text="@(Format(
          // Ignore ) and " inside this comment
          "Send to user"))" class="wide" />
      """
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      <Widget class="wide"
              Text="@(Format(
          // Ignore ) and " inside this comment
          "Send to user"))" />
      """
    And the rewritten source should pass check and remain unchanged by another fix

  @razorstyle
  Scenario: Nested components with complex attributes still receive child content formatting
    Given the Razor source is
      """
      <Panel><Panel Text="@($"Send ) > to {user}")" /></Panel>
      """
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      <Panel>
          <Panel Text="@($"Send ) > to {user}")" />
      </Panel>
      """
    And the rewritten source should pass check and remain unchanged by another fix

  @razorstyle
  Scenario Outline: Incomplete Razor expressions are not formatted
    Given the Razor source is
      """
      <Widget Text=<value> class="wide" />
      """
    When the Razor tags are scanned
    Then 0 Razor start tags should be found
    When RazorStyle fix runs
    Then the Razor source should not be rewritten

    Examples:
      | value                         |
      | "@($"Send to {user}")         |
      | "@(Format("Send to user")"    |
      | @(_ => Format("Send to user") |
