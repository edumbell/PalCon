
function AI(ownerId) {
    this.ownerId = ownerId;
    this.planetData = [];
    
    
    this.init = function () {
        this.persistentPlanetData = Array.apply(null, Array(planets.length));
        for (var idx in this.persistentPlanetData)
            this.persistentPlanetData[idx] = { lastSent: -4 };

        this.enmity = Array.apply(null, Array(numPlayers+1 )).map(Number.prototype.valueOf, 1);
        this.attackPref = Array.apply(null, Array(numPlayers + 1)).map(Number.prototype.valueOf, 1);
        this.might = Array.apply(null, Array(numPlayers + 1)).map(Number.prototype.valueOf, 1);
    };
    function PlanetData(planet) {
        this.planet = planet;
        this.planetId = planet.index;
        this.owner = planet.owner;
        this.overpop = 0;
        this.actualDefence = planet.pop;
        this.actualAttack = 0;
        this.needsHelp = 0;
        this.influence = Array.apply(null, Array(numPlayers + 1)).map(Number.prototype.valueOf, 0);
        this.influenceAddedThisStep = Array.apply(null, Array(numPlayers + 1)).map(Number.prototype.valueOf, 0);
        this.influence[this.owner] = planet.pop;
        this.desirability = planet.growth;
        this.aiInfluence = 0;
        this.planetInfluence = 0;
        this.otherInfluence = 0;
        this.goodTarget = 0;
        this.goodRallyPoint = 0;
    }

    this.processFleetAttack = function (f) {
        if (f.destination.owner == this.ownerId)
            this.enmity[f.owner] += (f.pop + 1) / (f.pop + f.destination.pop + 10);
        else if (f.destination.owner == this.topEnemy)
            this.enmity[f.owner] -= (f.pop + 1) / (f.pop + f.destination.pop + 10) / 2;
        else {
            this.enmity[f.owner] -= (f.pop + 1) / (f.pop + f.destination.pop + 10) / 7;
            this.enmity[f.destination.owner] -= (f.pop + 1) / (f.pop + f.destination.pop + 10) / 11;
        }
    }

    this.doInfluenceStep = function (multiplier) {
        if (!multiplier)
            multiplier = 50;
        var matrix;
        var p;
        var otherInfluence = 0;
        var spareInfluence = 0;
        var thisPlanetMultiplier;
        var i;
        var j;
        for (var idx in planets) {
            p = this.planetData[idx];
            if (p.owner > 0) {
                otherInfluence = 0;
                matrix = distanceMatrix[idx];
                //if (matrix.planetId != idx) alert('error ' + idx);
                for (i in p.influence) {
                    if (i > 0 && i != p.owner)
                        otherInfluence += p.influence[i];
                }
                spareInfluence = p.influence[p.owner] - otherInfluence;
                thisPlanetMultiplier = multiplier / distanceMatrixSumInverts[idx] / 100;
                if (spareInfluence > 0) {
                    for (j in matrix) {
                        toPlanetData = this.planetData[matrix[j].planetId];
                        amt = spareInfluence * thisPlanetMultiplier / (matrix[j].dist + 30);
                        toPlanetData.influenceAddedThisStep[p.owner] += amt;
                        p.influenceAddedThisStep[p.owner] -= amt * .8; // NB CREATES MORE INFLUENCE THAN POP
                    }
                }

            }
        }

        for (var idx in planets) {
            p = this.planetData[idx];
            for (i in p.influenceAddedThisStep) {
                p.influence[i] += p.influenceAddedThisStep[i];
                p.influenceAddedThisStep[i] = 0;
            }
        }

        for (var idx in this.enmity) {
            this.enmity[idx] = this.enmity[idx] * .995;
        }
    }

    this.calcFinalInfluenceByType = function () {
        var i;
        var j;
        for (var idx in planets) {
            p = this.planetData[idx];
            p.aiInfluence = 0;
            p.planetInfluence = 0;
            p.otherInfluence = 0;
            for (i in p.influence) {
                if (i == p.owner) {
                    p.planetInfluence += p.influence[i];
                }
                if (i == this.ownerId)
                    p.aiInfluence += p.influence[i];
                if (i != p.owner && i != this.ownerId)
                    p.otherInfluence += p.influence[i];
            }
            if (p.owner == this.ownerId) {
                p.needsHelp =
                    Math.max(p.actualAttack / p.actualDefence,
                    p.otherInfluence / p.planetInfluence)
                    * (.5 + p.desirability / 4);

                if (p.otherInfluence / p.planetInfluence > 5)
                    p.needsHelp = 0;

                p.overpop = (p.planet.pop) / (p.needsHelp + .1);
                if (p.planet.pop > p.planet.fullpop)
                    p.overpop = p.overpop * 2;

            }
            else {
                if (p.owner != this.ownerId) {
                    if (p.actualAttack < p.actualDefence) {
                        p.goodTarget = Math.min(p.aiInfluence / (p.planetInfluence + p.otherInfluence * .6), 2);

                        if (p.goodTarget > .5) {
                            p.goodTarget = p.goodTarget * (.5 + p.desirability / 2);
                        }

                        if (p.goodTarget > .3) {
                            for (j in distanceMatrix[p.planetId]) {
                                dmd = distanceMatrix[p.planetId][j];
                                if (this.planetData[dmd.planetId].owner == this.ownerId) {
                                    this.planetData[dmd.planetId].goodRallyPoint += p.goodTarget * 100 / (dmd.dist + 30);
                                }
                                else {
                                    this.planetData[dmd.planetId].goodTarget += p.goodTarget * 20 / (dmd.dist + 30); // if not owned and would be good rally point, might be good target
                                }
                            }
                        }
                        p.goodTarget = p.goodTarget * (.5 + Math.max(this.attackPref[p.owner] / 4, 4));

                        p.goodTarget *= (1 + .5 * Math.random());
                    }
                }

            }


        }

    }

    this.checkAndSend = function (fromId, to, percent) {
        //if (fromId == 1)
        //    console.log('sending from 1 ' + percent);
        if (this.persistentPlanetData[fromId].lastSent >= turnId - 4)
            return;
        tryCreateCommand(planets[fromId], planets[to], percent, this.ownerId);
        //if (fromId > 50)
        //    console.log('trying to send from ' + fromId);
        //console.log(fromId);
        this.persistentPlanetData[fromId].lastSent = turnId;
    }

    this.doTurn1 = function () {
        // own planets:  other-influence(danger) vs  self-influence(safety)
        // other planets:  danger vs greed vs self-influence
        // send ships to:   short term other>self; long term self>=other
        this.planetData = [];
        this.might = Array.apply(null, Array(numPlayers + 3)).map(Number.prototype.valueOf, 0);
        var p;
        for (var idx in planets) {
            p = planets[idx];
            this.planetData.push(new PlanetData(p));
            this.might[p.owner] += p.pop + p.growth * 10;
        }
        for (var idx in fleets) {
            f = fleets[idx];
            p = this.planetData[f.destination.index];

            mult = 100 / (Math.max(50, f.distToGo) + 50);
            if (f.owner == p.owner) {

                p.actualDefence += f.pop * mult;
            }
            else {
                mult = mult * .8;
                p.actualAttack += f.pop * mult;
            }
            p.influence[f.owner] += f.pop * mult;
            this.might[f.owner] += f.pop;
        }

        this.topEnemy = -1;
        var topEnemyMight = -1;
        for (i in this.might) {
            if (i > 0 && i != this.ownerId) {
                this.attackPref[i] = Math.max(this.enmity[i], 0);
                if (this.might[i] > topEnemyMight) {
                    this.topEnemy = i;
                    topEnemyMight = this.might[i];
                }
            }
        }
        if (this.topEnemy > 0) {
            this.attackPref[this.topEnemy] = this.attackPref[this.topEnemy] * 2 + .8;
        }

        this.doInfluenceStep(40);


    };

    this.doTurn2 = function () {
        this.doInfluenceStep(60);
    };

    this.doTurn3 = function () {
        this.doInfluenceStep(70);
    };

    this.doTurnFinal = function () {
        if (Math.random() * Math.random() * 100 > lethargy) {

            this.calcFinalInfluenceByType();

            var bestTargets = this.planetData.concat().sort(function (a, b) { return a.goodTarget > b.goodTarget ? -1 : 1 });
            var bestRallies = this.planetData.concat().sort(function (a, b) { return a.goodRallyPoint > b.goodRallyPoint ? -1 : 1 });
            var mostNeed = this.planetData.concat().sort(function (a, b) { return a.needsHelp > b.needsHelp ? -1 : 1 });
            var overpops = this.planetData.concat().sort(function (a, b) { return a.overpop > b.overpop ? -1 : 1 });
            var need = mostNeed[0];

            if (need.needsHelp > .8) {


                for (var idx in distanceMatrix[need.planetId]) {
                    var dp = distanceMatrix[need.planetId][idx].planetId;

                    if (planets[dp].owner == this.ownerId) {


                        if (this.persistentPlanetData[dp].lastSent <= turnId - 5) {

                            if ((this.planetData[dp].needsHelp < .8) && this.planetData[dp].planet.pop > need.planet.pop / 5) {

                                var percent = Math.min(100 * (need.needsHelp - .6) * need.planet.pop / planets[dp].pop, 80);
                                percent = Math.max(percent, 25);
                                if (percent * planets[dp].pop > .1 * need.planet.pop) {
                                    this.checkAndSend(dp, need.planetId, percent);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            var overpop = overpops[0];
            if (this.persistentPlanetData[overpop.planetId].lastSent <= turnId - 19) {
                //console.log('can send from most-overpop');
                if (overpop.overpop > 200) {
                    //console.log('overpop:')
                    //console.log(overpop);
                    for (var idx in distanceMatrix[overpop.planetId]) {

                        var dp = distanceMatrix[overpop.planetId][idx].planetId;
                        if (planets[dp].owner == this.ownerId) {
                            //console.log('overpop recipient? ');
                            //console.log(this.planetData[dp]);
                            if (
                                this.planetData[dp].goodRallyPoint > 10 ||
                                this.planetData[dp].overpop < overpop.overpop * .5 * Math.random()) {
                                this.checkAndSend(overpop.planetId, dp, 50);
                                break;

                            }
                        }
                    }
                }
            }
            for (var r in [0, 1]) {
                var rally = bestRallies[r];
                for (var idx in distanceMatrix[rally.planetId]) {
                    var dp = distanceMatrix[rally.planetId][idx].planetId;
                    if (planets[dp].owner == this.ownerId) {
                        if (planets[dp].pop > rally.planet.pop / 5) {
                            if (this.persistentPlanetData[dp].lastSent <= turnId - 15) {
                                if (this.planetData[dp].needsHelp <= 0 && planets[dp].pop > 10) {

                                    this.checkAndSend(dp, rally.planetId, 50);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            var target = bestTargets[0];
            for (var idx in distanceMatrix[target.planetId]) {

                var dp = distanceMatrix[target.planetId][idx].planetId;
                if (planets[dp].owner == this.ownerId) {
                    if (this.persistentPlanetData[dp].lastSent <= turnId - 13) {

                        if ((this.planetData[dp].needsHelp <= .7) && (planets[dp].pop > 10) && this.planetData[dp].planet.pop >= target.planetInfluence / 2) {
                            this.checkAndSend(dp, target.planetId, 60);
                            break;
                        }
                    }
                }
            }


            if (this.ownerId == logAI) {
                console.log('----targets--');
                //console.log(this.planetData);
                console.log(bestTargets);
                console.log('----rallypoints--');
                console.log(bestRallies);
                console.log('----over-populated--');
                console.log(overpops);
                console.log(this);
                logAI = 0;


            }
        }
    };

}
