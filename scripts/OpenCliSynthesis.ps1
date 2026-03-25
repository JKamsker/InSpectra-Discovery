function Get-XmlAttributeValue {
    param(
        [AllowNull()][object]$Node,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Node) {
        return $null
    }

    $property = $Node.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

function Get-XmlElements {
    param(
        [AllowNull()][object]$Node,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Node) {
        return @()
    }

    $property = $Node.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }

    return @($property.Value)
}

function ConvertTo-XmlBoolean {
    param(
        [AllowNull()][string]$Value,
        [bool]$Default = $false
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return $Value.Trim().ToLowerInvariant() -eq 'true'
}

function Get-CollectionCount {
    param([AllowNull()][object]$Value)
    return @($Value).Count
}

function Get-XmlDescriptionText {
    param([AllowNull()][object]$Node)

    $descriptionNode = Get-XmlElements -Node $Node -Name 'Description' | Select-Object -First 1
    if ($null -eq $descriptionNode) {
        return $null
    }

    $value = if ($descriptionNode.PSObject.Properties['InnerText']) {
        [string]$descriptionNode.InnerText
    }
    else {
        [string]$descriptionNode
    }
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Get-SimplifiedClrTypeName {
    param([AllowNull()][string]$ClrType)

    if ([string]::IsNullOrWhiteSpace($ClrType)) {
        return $null
    }

    $trimmed = $ClrType.Trim()
    if ($trimmed -match '^System\.Nullable`1\[\[(?<inner>.+)\]\]$') {
        $inner = [string]$matches['inner']
        $innerName = if ($inner -match '^(?<name>[^,\[]+)') { [string]$matches['name'] } else { $inner }
        return "System.Nullable<$innerName>"
    }

    return $trimmed
}

function Get-OptionAliasParts {
    param(
        [AllowNull()][string]$ShortValue,
        [AllowNull()][string]$LongValue
    )

    $longParts = @(
        ($LongValue -split ',') |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    $shortParts = @(
        ($ShortValue -split ',') |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    $primaryName = $null
    $aliases = [System.Collections.Generic.List[string]]::new()

    if ((Get-CollectionCount $longParts) -gt 0) {
        $primaryName = '--' + $longParts[0]
        foreach ($alias in $longParts | Select-Object -Skip 1) {
            $aliases.Add('--' + $alias)
        }
        foreach ($alias in $shortParts) {
            $aliases.Add('-' + $alias)
        }
    }
    elseif ((Get-CollectionCount $shortParts) -gt 0) {
        $primaryName = '-' + $shortParts[0]
        foreach ($alias in $shortParts | Select-Object -Skip 1) {
            $aliases.Add('-' + $alias)
        }
    }

    return [ordered]@{
        name = $primaryName
        aliases = @($aliases)
    }
}

function New-OpenCliOptionArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ClrType,

        [Parameter(Mandatory = $true)]
        [string]$Kind,

        [AllowNull()][string]$Value
    )

    $isNullableBool = $ClrType -eq 'System.Nullable<System.Boolean>' -or $ClrType -like 'System.Nullable<System.Boolean>*'
    $needsArgument = $Kind -ne 'flag' -or $isNullableBool
    if (-not $needsArgument) {
        return $null
    }

    $argumentName = if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq 'NULL') { 'VALUE' } else { $Value }
    return [ordered]@{
        name = $argumentName
        required = $true
        arity = [ordered]@{
            minimum = 1
            maximum = 1
        }
        metadata = @(
            [ordered]@{
                name = 'ClrType'
                value = $ClrType
            }
        )
    }
}

function Convert-XmldocOptionToOpenCliOption {
    param([Parameter(Mandatory = $true)][object]$OptionNode)

    $aliasParts = Get-OptionAliasParts `
        -ShortValue (Get-XmlAttributeValue -Node $OptionNode -Name 'Short') `
        -LongValue (Get-XmlAttributeValue -Node $OptionNode -Name 'Long')

    if ([string]::IsNullOrWhiteSpace($aliasParts.name)) {
        return $null
    }

    $clrType = Get-SimplifiedClrTypeName (Get-XmlAttributeValue -Node $OptionNode -Name 'ClrType')
    $kind = [string](Get-XmlAttributeValue -Node $OptionNode -Name 'Kind')
    $argument = New-OpenCliOptionArgument `
        -ClrType $clrType `
        -Kind $kind `
        -Value (Get-XmlAttributeValue -Node $OptionNode -Name 'Value')

    $option = [ordered]@{
        name = $aliasParts.name
        required = ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $OptionNode -Name 'Required')
    }

    if ((Get-CollectionCount $aliasParts.aliases) -gt 0) {
        $option.aliases = $aliasParts.aliases
    }

    if ($null -ne $argument) {
        $option.arguments = @($argument)
    }

    $description = Get-XmlDescriptionText -Node $OptionNode
    if (-not [string]::IsNullOrWhiteSpace($description)) {
        $option.description = $description
    }

    $option.recursive = ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $OptionNode -Name 'Recursive')
    $option.hidden = ConvertTo-XmlBoolean `
        (Get-XmlAttributeValue -Node $OptionNode -Name 'Hidden') `
        -Default:(ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $OptionNode -Name 'IsHidden'))

    return $option
}

function Convert-XmldocArgumentToOpenCliArgument {
    param([Parameter(Mandatory = $true)][object]$ArgumentNode)

    $clrType = Get-SimplifiedClrTypeName (Get-XmlAttributeValue -Node $ArgumentNode -Name 'ClrType')
    $argument = [ordered]@{
        name = [string](Get-XmlAttributeValue -Node $ArgumentNode -Name 'Name')
        required = ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $ArgumentNode -Name 'Required')
        arity = [ordered]@{
            minimum = 1
            maximum = 1
        }
    }

    $description = Get-XmlDescriptionText -Node $ArgumentNode
    if (-not [string]::IsNullOrWhiteSpace($description)) {
        $argument.description = $description
    }

    $argument.hidden = ConvertTo-XmlBoolean `
        (Get-XmlAttributeValue -Node $ArgumentNode -Name 'Hidden') `
        -Default:(ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $ArgumentNode -Name 'IsHidden'))

    if (-not [string]::IsNullOrWhiteSpace($clrType)) {
        $argument.metadata = @(
            [ordered]@{
                name = 'ClrType'
                value = $clrType
            }
        )
    }

    return $argument
}

function Test-ExampleStartSequence {
    param(
        [string[]]$Tokens,
        [int]$Index,
        [string[]]$Sequence
    )

    $tokensArray = @($Tokens)
    $sequenceArray = @($Sequence)

    if ((Get-CollectionCount $sequenceArray) -eq 0) {
        return $false
    }

    if (($Index + (Get-CollectionCount $sequenceArray)) -gt (Get-CollectionCount $tokensArray)) {
        return $false
    }

    for ($offset = 0; $offset -lt (Get-CollectionCount $sequenceArray); $offset++) {
        if ($tokensArray[$Index + $offset] -ne $sequenceArray[$offset]) {
            return $false
        }
    }

    return $true
}

function Convert-XmldocExamplesToOpenCliExamples {
    param(
        [Parameter(Mandatory = $true)]
        [object]$CommandNode,

        [string[]]$CommandPath
    )

    $commandName = [string](Get-XmlAttributeValue -Node $CommandNode -Name 'Name')
    if ($commandName -eq '__default_command') {
        return @()
    }

    $examplesNode = Get-XmlElements -Node $CommandNode -Name 'Examples' | Select-Object -First 1
    $tokens = @(
        (Get-XmlElements -Node $examplesNode -Name 'Example') |
            ForEach-Object {
                $token = Get-XmlAttributeValue -Node $_ -Name 'commandLine'
                if ([string]::IsNullOrWhiteSpace($token)) {
                    $token = Get-XmlAttributeValue -Node $_ -Name 'CommandLine'
                }

                if (-not [string]::IsNullOrWhiteSpace($token)) {
                    $token.Trim()
                }
            }
    )

    $tokens = @($tokens)
    if ((Get-CollectionCount $tokens) -eq 0) {
        return @()
    }

    $commandPathArray = @($CommandPath)
    $startSequence = if ((Get-CollectionCount $commandPathArray) -gt 0) { $commandPathArray } else { @($commandName) }
    $examples = [System.Collections.Generic.List[string]]::new()
    $index = 0

    while ($index -lt (Get-CollectionCount $tokens)) {
        $parts = [System.Collections.Generic.List[string]]::new()

        if (Test-ExampleStartSequence -Tokens $tokens -Index $index -Sequence $startSequence) {
            foreach ($segment in $startSequence) {
                $parts.Add($segment)
            }
            $index += Get-CollectionCount $startSequence
        }
        else {
            $parts.Add($tokens[$index])
            $index++
        }

        while ($index -lt (Get-CollectionCount $tokens)) {
            if (Test-ExampleStartSequence -Tokens $tokens -Index $index -Sequence $startSequence) {
                break
            }

            $parts.Add($tokens[$index])
            $index++
        }

        $example = ($parts.ToArray() -join ' ').Trim()
        if (-not [string]::IsNullOrWhiteSpace($example)) {
            $examples.Add($example)
        }
    }

    return @($examples)
}

function Convert-XmldocCommandToOpenCliCommand {
    param(
        [Parameter(Mandatory = $true)]
        [object]$CommandNode,

        [string[]]$ParentPath = @()
    )

    $commandName = [string](Get-XmlAttributeValue -Node $CommandNode -Name 'Name')
    $commandPath = @($ParentPath + $commandName)

    $command = [ordered]@{
        name = $commandName
    }

    $parametersNode = Get-XmlElements -Node $CommandNode -Name 'Parameters' | Select-Object -First 1
    $options = @(
        (Get-XmlElements -Node $parametersNode -Name 'Option') |
            ForEach-Object { Convert-XmldocOptionToOpenCliOption -OptionNode $_ } |
            Where-Object { $null -ne $_ }
    )
    if ((Get-CollectionCount $options) -gt 0) {
        $command.options = $options
    }

    $arguments = @(
        (Get-XmlElements -Node $parametersNode -Name 'Argument') |
            ForEach-Object { Convert-XmldocArgumentToOpenCliArgument -ArgumentNode $_ }
    )
    if ((Get-CollectionCount $arguments) -gt 0) {
        $command.arguments = $arguments
    }

    $description = Get-XmlDescriptionText -Node $CommandNode
    if (-not [string]::IsNullOrWhiteSpace($description)) {
        $command.description = $description
    }

    $children = @(
        (Get-XmlElements -Node $CommandNode -Name 'Command') |
            ForEach-Object { Convert-XmldocCommandToOpenCliCommand -CommandNode $_ -ParentPath $commandPath }
    )
    if ((Get-CollectionCount $children) -gt 0) {
        $command.commands = $children
    }

    $command.hidden = if ($commandName -eq '__default_command') {
        $true
    }
    else {
        ConvertTo-XmlBoolean `
            (Get-XmlAttributeValue -Node $CommandNode -Name 'Hidden') `
            -Default:(ConvertTo-XmlBoolean (Get-XmlAttributeValue -Node $CommandNode -Name 'IsHidden'))
    }

    $command.examples = Convert-XmldocExamplesToOpenCliExamples -CommandNode $CommandNode -CommandPath $commandPath
    return $command
}

function Convert-XmldocToOpenCliDocument {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$XmlDocument,

        [Parameter(Mandatory = $true)]
        [string]$Title,

        [string]$Version = '1.0'
    )

    $rootCommands = @(
        (Get-XmlElements -Node $XmlDocument.Model -Name 'Command') |
            ForEach-Object { Convert-XmldocCommandToOpenCliCommand -CommandNode $_ }
    )

    $defaultCommand = $rootCommands | Where-Object name -eq '__default_command' | Select-Object -First 1
    $defaultOptions = $null
    if ($defaultCommand -and $defaultCommand.PSObject.Properties['options']) {
        $defaultOptions = $defaultCommand.options
    }

    return [ordered]@{
        opencli = '0.1-draft'
        info = [ordered]@{
            title = $Title
            version = $Version
        }
        'x-inspectra' = [ordered]@{
            synthesized = $true
            artifactSource = 'synthesized-from-xmldoc'
            sourceArtifact = 'xmldoc.xml'
            generator = 'InSpectra.Discovery'
        }
        options = if ($defaultOptions) { $defaultOptions } else { $null }
        commands = $rootCommands
    }
}
