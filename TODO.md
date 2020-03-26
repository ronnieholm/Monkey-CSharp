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
    Returnstatement -> "return"? Expression ";"
    BlockStatement -> "{" Statements? "}"
    Expressionstatement -> Expression ";"

    Expression -> Equality
    Equality -> Comparison ("==" | "!=") Comparison
    Comparison -> Addition ("<" | ">") Addition
    Addition -> Multiplication ("+"|"*") Multiplication
    Multiplication -> Unary ("*"|"/") Unary
    Unary -> ( "!" | "-") Unary | Primary

    Primary -> INTEGER | STRING | Idenfier | "false" | "true" | "null" | "(" expression ")"
