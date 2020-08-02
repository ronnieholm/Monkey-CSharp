# TODO

- Implement regular recursive descent parser for grammar

    Index expression?
    Array literal?
    Function literals
    Call expression

    Program -> Statements*
    Statements -> LetStatement | IfStatement | ReturnStatement | ExpressionStatement
    LetStatement -> Identifier "=" Expression ";"
    IfStatement -> "if" "(" Expression ")" BlockStatement ("else" BlockStatement)?
    ReturnStatement -> "return"? Expression ";"
    BlockStatement -> "{" Statements? "}"
    ExpressionStatement -> Expression ";"

    Expression -> Equality
    Equality -> Comparison ("==" | "!=") Comparison
    Comparison -> Addition ("<" | ">") Addition
    Addition -> Multiplication ("+"|"*") Multiplication
    Multiplication -> Unary ("*"|"/") Unary
    Unary -> ( "!" | "-") Unary | Primary

    Primary -> INTEGER | STRING | Identifier | "false" | "true" | "null" | "(" expression ")"
