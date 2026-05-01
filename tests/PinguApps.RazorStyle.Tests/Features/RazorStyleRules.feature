Feature: Razor style rules

  @razorstyle
  Scenario: Common start tag forms are scanned
    Given the Razor source is
      """
      <Foo />
      <Foo Param="Bar" />
      <Foo Param="Bar" Param2="Baz" />
      <Foo Param="@Value" />
      <Foo @onclick="HandleClick" />
      <input disabled />
      <MyComponent Foo="@(A + B)" Bar='Baz' />
      """
    When the Razor tags are scanned
    Then 7 Razor start tags should be found
    And tag 3 should have attributes "Param=\"Bar\",Param2=\"Baz\""
    And tag 6 should have attributes "disabled"

  @razorstyle
  Scenario: Raw text scanning requires full closing tag name matches
    Given the Razor source is
      """
      <script>
          const value = "</scripture><Foo Param=\"Bar\" />";
      </script>
      <Bar />
      """
    When the Razor tags are scanned
    Then 2 Razor start tags should be found
    And tag 1 should be named "script"
    And tag 2 should be named "Bar"

  @razorstyle
  Scenario: Attribute wrapping accepts valid markup
    Given the Razor source is
      """
      <LongName Param="Bar"
                Param2="Baz"
                Param3="Qux">
      </LongName>
      """
    When RazorStyle check runs
    Then no RazorStyle diagnostics should be reported

  @razorstyle
  Scenario: Attribute wrapping reports invalid markup
    Given the Razor source is
      """
      <Foo Param="Bar" Param2="Baz" />
      """
    When RazorStyle check runs
    Then RazorStyle should report "RS0001"
    And RazorStyle should report "Each attribute after the first must begin on a new line."

  @razorstyle
  Scenario: Attribute wrapping fixes invalid markup
    Given the Razor source is
      """
      <Foo   Param="Bar"    Param2="Baz"
        Param3="Qux"/>
      """
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      <Foo Param="Bar"
           Param2="Baz"
           Param3="Qux" />
      """

  @razorstyle
  Scenario: Child content must be on its own line
    Given the Razor source is
      """
      <span>Some text</span>
      """
    When RazorStyle fix runs
    Then RazorStyle should report "RS0002"
    And the rewritten Razor source should be
      """
      <span>
          Some text
      </span>
      """

  @razorstyle
  Scenario: Self closing tags satisfy child content rule
    Given the Razor source is
      """
      <span />
      <span class="foo" />
      <span id="bar"
            class="baz" />
      """
    When RazorStyle check runs
    Then no RazorStyle diagnostics should be reported

  @razorstyle
  Scenario: Child content line rule handles self closing child tags with the same name
    Given the Razor source is
      """
      <Panel><Panel /></Panel>
      """
    When RazorStyle fix runs
    Then RazorStyle should report "RS0002"
    And the rewritten Razor source should be
      """
      <Panel>
          <Panel />
      </Panel>
      """

  @razorstyle
  Scenario: Child content line rule requires full closing tag name matches
    Given the Razor source is
      """
      <Panel><PanelHeader>
          Heading
      </PanelHeader></Panel>
      """
    When RazorStyle fix runs
    Then RazorStyle should report "RS0002"
    And the rewritten Razor source should be
      """
      <Panel>
          <PanelHeader>
              Heading
          </PanelHeader>
      </Panel>
      """

  @razorstyle
  Scenario: Child content line rule ignores closing tag text inside raw text children
    Given the Razor source is
      """
      <div><script>
          const value = "</div>";
      </script></div>
      """
    When RazorStyle fix runs
    Then RazorStyle should report "RS0002"
    And the rewritten Razor source should be
      """
      <div>
          <script>
              const value = "</div>";
          </script>
      </div>
      """

  @razorstyle
  Scenario: Attributes are ordered before wrapping
    Given the Razor source is
      """
      <button data-track="save" disabled class="btn" @onclick="Save" id="save-button" />
      """
    When RazorStyle fix runs
    Then RazorStyle should report "RS0003"
    And the rewritten Razor source should be
      """
      <button id="save-button"
              class="btn"
              @onclick="Save"
              data-track="save"
              disabled />
      """

  @razorstyle
  Scenario: Attribute ordering preserves existing wrapping when wrapping is disabled
    Given the Razor source is
      """
      <button class="btn" id="save-button" />
      """
    And RazorStyle rule "RS0001" is disabled
    When RazorStyle fix runs
    Then RazorStyle should report "RS0003"
    And the rewritten Razor source should be
      """
      <button id="save-button" class="btn" />
      """

  @razorstyle
  Scenario: Full file processing preserves unrelated content and line endings
    Given the Razor source is
      """
      @code {
          private string Value = "A";
      }

      <Foo Param="Bar" Param2="Baz" />
      <Bar />
      <Baz Param="@Value">
          Child content
      </Baz>
      """
    When RazorStyle fix runs
    Then the rewritten Razor source should be
      """
      @code {
          private string Value = "A";
      }

      <Foo Param="Bar"
           Param2="Baz" />
      <Bar />
      <Baz Param="@Value">
          Child content
      </Baz>
      """

  @razorstyle
  Scenario: Disabled rules are not applied
    Given the Razor source is
      """
      <Foo Param="Bar" Param2="Baz" />
      """
    And RazorStyle rule "RS0001" is disabled
    When RazorStyle check runs
    Then no RazorStyle diagnostics should be reported
    When RazorStyle fix runs
    Then the Razor source should not be rewritten
