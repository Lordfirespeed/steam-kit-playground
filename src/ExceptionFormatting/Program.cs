using System;

DoSomethingBoring();
return;

void DoSomethingInteresting()
{
    throw new InvalidOperationException("you can't do that!");
}

void DoSomethingElse()
{
    try {
        DoSomethingInteresting();
    }
    catch (Exception ex) {
        throw new Exception("oh dear, i had a tantrum", ex);
    }
}

void DoSomethingBoring()
{
    try {
        DoSomethingElse();
    }
    catch (Exception ex) {
        throw new Exception("silly me!", ex);
    }
}
