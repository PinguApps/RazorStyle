Feature: Component checks through the CLI
    The standalone check command checks the same component rules as a build.

Scenario: Standalone check identifies components in ordinary C# files
    Given a restored component project for the CLI
    And the CLI component is in an ordinary C# file
    When RazorStyle checks the component project
    Then the CLI exits with 1
    And the CLI reports "PARS0004"
    And the CLI reports "PARS0005"

Scenario: Standalone check catches both rules using the generated component identity
    Given a restored component project for the CLI
    When RazorStyle checks the component project
    Then the CLI exits with 1
    And the CLI reports "PARS0004"
    And the CLI reports "PARS0005"

Scenario: CLI switches disable rules independently
    Given a restored component project for the CLI
    And the CLI arguments are "--disable PARS0004"
    When RazorStyle checks the component project
    Then the CLI exits with 1
    And the CLI does not report "PARS0004"
    And the CLI reports "PARS0005"

Scenario: Project properties disable rules independently
    Given a restored component project for the CLI
    And the CLI project sets "DisablePARS0005" to true
    When RazorStyle checks the component project
    Then the CLI exits with 1
    And the CLI reports "PARS0004"
    And the CLI does not report "PARS0005"

Scenario: Both project switches allow a clean check
    Given a restored component project for the CLI
    And the CLI project sets "DisablePARS0004" to true
    And the CLI project sets "DisablePARS0005" to true
    When RazorStyle checks the component project
    Then the CLI exits with 0
    And the CLI does not report "PARS0004"
    And the CLI does not report "PARS0005"

Scenario: Editorconfig suppression is respected
    Given a restored component project for the CLI
    And the CLI project suppresses component rules in editorconfig
    When RazorStyle checks the component project
    Then the CLI exits with 0
    And the CLI does not report "PARS0004"
    And the CLI does not report "PARS0005"

Scenario: Compiler errors cannot silently pass checking
    Given a restored component project for the CLI
    And the CLI component has a compiler error
    When RazorStyle checks the component project
    Then the CLI exits with 2
    And the CLI reports "Cannot check component rules until the project compiles"

Scenario: Missing project context cannot silently pass checking
    Given a restored component project for the CLI
    And the CLI component has no project
    When RazorStyle checks the component project
    Then the CLI exits with 2
    And the CLI reports "No C# project found"
